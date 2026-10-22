
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Domain.Envios;

public class Envio : AuditableEntity, ITenantEntity
{
    private readonly List<Bulto> _bultos = [];
    private readonly List<EventoEnvio> _eventos = [];
    private readonly List<IntentoEntrega> _intentos = [];

    public Guid OperadorId { get; private set; }
    public Guid CuentaComercialId { get; private set; }
    public string ReferenciaExterna { get; private set; } = string.Empty;
    public Modalidad Modalidad { get; private set; }
    public EstadoEnvio Estado { get; private set; }
    public Destinatario Destinatario { get; private set; } = null!;
    public Direccion Direccion { get; private set; } = null!;
    public DateTimeOffset FechaAlta { get; private set; }
    public DateOnly? FechaEntregaComprometida { get; private set; }
    public TimeOnly? HoraEntregaDesde { get; private set; }
    public TimeOnly? HoraEntregaHasta { get; private set; }
    public decimal? TarifaCalculada { get; private set; }
    public Guid? VersionTarifariaId { get; private set; }
    public Guid? VersionReglasOperativasId { get; private set; }

    public int IntentosRealizados { get; private set; }

    public DateTimeOffset? FechaReprogramada { get; private set; }
    public string? FranjaReprogramada { get; private set; }

    public Guid? UltimoMotivoNoEntregaId { get; private set; }
    public bool UltimoMotivoRequiereDevolucion { get; private set; }

    public IReadOnlyCollection<Bulto> Bultos => _bultos;
    public IReadOnlyCollection<EventoEnvio> Eventos => _eventos;
    public IReadOnlyCollection<IntentoEntrega> Intentos => _intentos;

    public decimal PesoTotalKg => _bultos.Sum(b => b.PesoKg);
    public decimal VolumenTotalMetrosCubicos =>
        _bultos.Sum(b => b.VolumenMetrosCubicos);

    private Envio() { }

    public static Envio Crear(
        CuentaComercial cuenta,
        string referenciaExterna,
        Modalidad modalidad,
        Destinatario destinatario,
        Direccion direccion,
        IReadOnlyList<NuevoBulto> bultos,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(destinatario);
        ArgumentNullException.ThrowIfNull(direccion);

        if (!cuenta.Activa)
            throw new InvalidOperationException(
                "La cuenta comercial no está activa.");

        if (string.IsNullOrWhiteSpace(referenciaExterna))
            throw new ArgumentException(
                "La referencia externa es obligatoria.",
                nameof(referenciaExterna));

        if (bultos is null || bultos.Count == 0)
            throw new ArgumentException(
                "Un envío debe tener al menos un bulto.",
                nameof(bultos));

        var referencia = referenciaExterna.Trim();

        var envio = new Envio
        {
            Id = Guid.NewGuid(),
            OperadorId = cuenta.OperadorId,
            CuentaComercialId = cuenta.Id,
            ReferenciaExterna = referencia,
            Modalidad = modalidad,
            Estado = EstadoEnvio.Admitido,
            Destinatario = destinatario,
            Direccion = direccion,
            FechaAlta = ahora
        };

        for (var i = 0; i < bultos.Count; i++)
        {
            envio._bultos.Add(
                Bulto.Crear($"{referencia}-{i + 1}", bultos[i]));
        }

        return envio;
    }

    public void AplicarTarifa(
        Guid versionTarifariaId,
        decimal tarifaCalculada)
    {
        if (versionTarifariaId == Guid.Empty)
            throw new ArgumentException(
                "La versión tarifaria es obligatoria.",
                nameof(versionTarifariaId));

        if (tarifaCalculada < 0)
            throw new ArgumentException(
                "La tarifa calculada no puede ser negativa.",
                nameof(tarifaCalculada));

        VersionTarifariaId = versionTarifariaId;
        TarifaCalculada = tarifaCalculada;
    }

    public void AsignarReglasOperativas(Guid versionReglasOperativasId)
    {
        if (versionReglasOperativasId == Guid.Empty)
            throw new ArgumentException(
                "La versión de reglas operativas es obligatoria.",
                nameof(versionReglasOperativasId));

        if (VersionReglasOperativasId.HasValue)
            throw new InvalidOperationException(
                "El envío ya tiene una versión de reglas operativas asignada.");

        VersionReglasOperativasId = versionReglasOperativasId;
    }
    
    public void EstablecerCompromisoEntrega(
        DateOnly fecha,
        TimeOnly horaDesde,
        TimeOnly horaHasta)
    {
        if (horaDesde >= horaHasta)
            throw new ArgumentException(
                "La hora de inicio debe ser anterior a la hora de fin.");

        if (Estado != EstadoEnvio.Admitido &&
            Estado != EstadoEnvio.EnDeposito)
            throw new InvalidOperationException(
                "No se puede modificar el compromiso de un envío en este estado.");

        FechaEntregaComprometida = fecha;
        HoraEntregaDesde = horaDesde;
        HoraEntregaHasta = horaHasta;
    }

    public void Aplicar(
        DisparadorEnvio disparador,
        VersionReglasOperativas reglas,
        DateTimeOffset ocurridoEn,
        string origen,
        string responsable,
        PruebaEntrega? pruebaEntrega = null,
        MotivoNoEntrega? motivoNoEntrega = null,
        string? evidenciaFallo = null,
        DateTimeOffset? fechaReprogramada = null,
        string? franjaReprogramada = null,
        string? justificacion = null,
        decimal? latitud = null,
        decimal? longitud = null)
    {
        ArgumentNullException.ThrowIfNull(reglas);

        if (VersionReglasOperativasId != reglas.Id ||
            reglas.OperadorId != OperadorId)
        {
            throw new InvalidOperationException(
                "Las reglas no corresponden a este envío.");
        }

        if (string.IsNullOrWhiteSpace(origen) ||
            string.IsNullOrWhiteSpace(responsable))
        {
            throw new ArgumentException(
                "El origen y el responsable son obligatorios.");
        }

        var maquina = MaquinaEstadosEnvio.Crear(
            () => Estado,
            nuevoEstado => Estado = nuevoEstado);

        if (!maquina.CanFire(disparador))
        {
            throw new InvalidOperationException(
                $"No se puede aplicar {disparador} desde el estado {Estado}.");
        }

        IntentoEntrega? nuevoIntento = null;

        switch (disparador)
        {
            case DisparadorEnvio.AsignarARuta:
                if (Estado == EstadoEnvio.EnDeposito &&
                    IntentosRealizados != 0)
                {
                    throw new InvalidOperationException(
                        "Después de un intento fallido debe reprogramarse.");
                }

                if (Estado == EstadoEnvio.Reprogramado &&
                    !FechaReprogramada.HasValue)
                {
                    throw new InvalidOperationException(
                        "El envío no tiene fecha de reprogramación.");
                }
                break;

            case DisparadorEnvio.Entregar:
                if (pruebaEntrega is null)
                    throw new InvalidOperationException(
                        "La entrega requiere una prueba de entrega.");

                if (IntentosRealizados >= reglas.MaximoIntentosEntrega)
                    throw new InvalidOperationException(
                        "Se alcanzó el máximo de intentos.");

                if (reglas.RequierePruebaEntrega &&
                    string.IsNullOrWhiteSpace(pruebaEntrega.ReferenciaFirma) &&
                    string.IsNullOrWhiteSpace(pruebaEntrega.ReferenciaFotografia))
                {
                    throw new InvalidOperationException(
                        "La prueba de entrega no cumple las reglas del operador.");
                }

                nuevoIntento = IntentoEntrega.RegistrarEntrega(
                    OperadorId,
                    Id,
                    IntentosRealizados + 1,
                    pruebaEntrega);
                break;

            case DisparadorEnvio.RegistrarIntentoFallido:
                if (IntentosRealizados >= reglas.MaximoIntentosEntrega)
                    throw new InvalidOperationException(
                        "Se alcanzó el máximo de intentos.");

                if (motivoNoEntrega is null ||
                    motivoNoEntrega.OperadorId != OperadorId ||
                    motivoNoEntrega.VersionReglasOperativasId != reglas.Id)
                {
                    throw new InvalidOperationException(
                        "El motivo no pertenece al catálogo de reglas del envío.");
                }

                nuevoIntento = IntentoEntrega.RegistrarFallo(
                    OperadorId,
                    Id,
                    IntentosRealizados + 1,
                    motivoNoEntrega.Id,
                    evidenciaFallo ?? string.Empty,
                    ocurridoEn);
                break;

            case DisparadorEnvio.RetornarADeposito:
                if (IntentosRealizados >= reglas.MaximoIntentosEntrega ||
                    UltimoMotivoRequiereDevolucion)
                {
                    throw new InvalidOperationException(
                        "Corresponde iniciar la devolución.");
                }
                break;

            case DisparadorEnvio.IniciarDevolucion:
                if (IntentosRealizados < reglas.MaximoIntentosEntrega &&
                    !UltimoMotivoRequiereDevolucion)
                {
                    throw new InvalidOperationException(
                        "Todavía no corresponde iniciar la devolución.");
                }
                break;

            case DisparadorEnvio.Reprogramar:
                if (IntentosRealizados == 0)
                    throw new InvalidOperationException(
                        "La reprogramación requiere un intento fallido previo.");

                if (!fechaReprogramada.HasValue ||
                    string.IsNullOrWhiteSpace(franjaReprogramada))
                {
                    throw new ArgumentException(
                        "Debe indicarse una nueva fecha y franja.");
                }

                var ultimoFallo = _intentos
                    .Where(i => !i.Entregado)
                    .OrderByDescending(i => i.OcurridoEn)
                    .FirstOrDefault();

                if (ultimoFallo is null ||
                    fechaReprogramada.Value <
                    ultimoFallo.OcurridoEn.AddHours(
                        reglas.HorasMinimasEntreIntentos))
                {
                    throw new InvalidOperationException(
                        "No se cumple el intervalo mínimo entre intentos.");
                }
                break;

            case DisparadorEnvio.DeclararExtraviado:
                if (string.IsNullOrWhiteSpace(justificacion))
                    throw new ArgumentException(
                        "Debe justificarse la declaración de extravío.");
                break;
        }

        var estadoAnterior = Estado;

        var evento = EventoEnvio.Crear(
            OperadorId,
            Id,
            estadoAnterior,
            ObtenerEstadoDestino(disparador),
            disparador,
            ocurridoEn,
            origen,
            responsable,
            latitud,
            longitud);

        maquina.Fire(disparador);

        if (nuevoIntento is not null)
        {
            _intentos.Add(nuevoIntento);
            IntentosRealizados++;

            if (!nuevoIntento.Entregado)
            {
                UltimoMotivoNoEntregaId = motivoNoEntrega!.Id;
                UltimoMotivoRequiereDevolucion =
                    motivoNoEntrega.RequiereDevolucion;
            }
        }

        if (disparador == DisparadorEnvio.Reprogramar)
        {
            FechaReprogramada = fechaReprogramada;
            FranjaReprogramada = franjaReprogramada!.Trim();
        }

        _eventos.Add(evento);
    }

    private EstadoEnvio ObtenerEstadoDestino(DisparadorEnvio disparador)
    {
        return disparador switch
        {
            DisparadorEnvio.RecibirEnDeposito => EstadoEnvio.EnDeposito,
            DisparadorEnvio.AsignarARuta => EstadoEnvio.AsignadoARuta,
            DisparadorEnvio.QuitarDeRuta => EstadoEnvio.EnDeposito,
            DisparadorEnvio.Despachar => EstadoEnvio.EnTransito,
            DisparadorEnvio.Entregar => EstadoEnvio.Entregado,
            DisparadorEnvio.RegistrarIntentoFallido => EstadoEnvio.NoEntregado,
            DisparadorEnvio.RetornarADeposito => EstadoEnvio.EnDeposito,
            DisparadorEnvio.IniciarDevolucion => EstadoEnvio.EnDevolucion,
            DisparadorEnvio.Reprogramar => EstadoEnvio.Reprogramado,
            DisparadorEnvio.ConfirmarDevolucion => EstadoEnvio.Devuelto,
            DisparadorEnvio.DeclararExtraviado => EstadoEnvio.Extraviado,
            DisparadorEnvio.Reaparecer => EstadoEnvio.EnDeposito,
            _ => throw new ArgumentOutOfRangeException(nameof(disparador))
        };
    }
}
