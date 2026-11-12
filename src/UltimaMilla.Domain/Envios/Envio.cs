
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Organizacion;
using System.Security.Cryptography;

namespace UltimaMilla.Domain.Envios;

public class Envio : AuditableEntity, ITenantEntity
{
    private readonly List<Bulto> _bultos = [];
    private readonly List<EventoEnvio> _eventos = [];
    private readonly List<IntentoEntrega> _intentos = [];

    public Guid OperadorId { get; private set; }
    public Guid CuentaComercialId { get; private set; }
    public string ReferenciaExterna { get; private set; } = string.Empty;
    public string? TokenSeguimientoHash { get; private set; }
    public string? TokenSeguimientoOriginal { get; private set; }
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

    public int CantidadBultos => _bultos.Count;

    public int BultosEntregados =>
        _bultos.Count(b => b.Estado == EstadoBulto.Entregado);

    public int BultosPendientes =>
        _bultos.Count(b => b.Estado == EstadoBulto.Pendiente);

    public int BultosEnDevolucion =>
        _bultos.Count(b => b.Estado == EstadoBulto.EnDevolucion);

    public int BultosDevueltos =>
        _bultos.Count(b => b.Estado == EstadoBulto.Devuelto);

    public decimal PesoPendienteKg =>
        _bultos
            .Where(b => b.Estado == EstadoBulto.Pendiente)
            .Sum(b => b.PesoKg);

    public decimal VolumenPendienteMetrosCubicos =>
        _bultos
            .Where(b => b.Estado == EstadoBulto.Pendiente)
            .Sum(b => b.VolumenMetrosCubicos);

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

        var tokenSeguimiento = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
        var envio = new Envio
        {
            Id = Guid.NewGuid(),
            OperadorId = cuenta.OperadorId,
            CuentaComercialId = cuenta.Id,
            ReferenciaExterna = referencia,
            TokenSeguimientoHash = Convert.ToHexString(
                SHA256.HashData(
                    Convert.FromHexString(tokenSeguimiento))),
            TokenSeguimientoOriginal = tokenSeguimiento,        
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
        TimeOnly horaHasta,
        VersionReglasOperativas? reglas = null,
        DateTimeOffset? inicioNuevaEntrega = null)
    {
        if (horaDesde >= horaHasta)
            throw new ArgumentException(
                "La hora de inicio debe ser anterior a la hora de fin.");

        if (Estado != EstadoEnvio.Admitido &&
            Estado != EstadoEnvio.EnDeposito &&
            Estado != EstadoEnvio.Reprogramado)
        {
            throw new InvalidOperationException(
                "No se puede modificar el compromiso de un envío en este estado.");
        }

        if (Estado == EstadoEnvio.Reprogramado)
        {
            if (reglas is null || !inicioNuevaEntrega.HasValue)
                throw new ArgumentException(
                    "La reprogramación requiere validar las reglas y la nueva fecha.");

            if (reglas.Id != VersionReglasOperativasId ||
                reglas.OperadorId != OperadorId)
            {
                throw new InvalidOperationException(
                    "Las reglas no corresponden al envío.");
            }

            var ultimoFallo = _intentos
                .Where(i => !i.Entregado)
                .OrderByDescending(i => i.OcurridoEn)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No se encontró el intento fallido anterior.");

            if (inicioNuevaEntrega.Value <
                ultimoFallo.OcurridoEn.AddHours(
                    reglas.HorasMinimasEntreIntentos))
            {
                throw new InvalidOperationException(
                    "No se cumple el intervalo mínimo entre intentos.");
            }

            if (IntentosRealizados >= reglas.MaximoIntentosEntrega ||
                UltimoMotivoRequiereDevolucion)
            {
                throw new InvalidOperationException(
                    "El envío no admite nuevos intentos de entrega.");
            }

            FechaReprogramada = inicioNuevaEntrega.Value.ToUniversalTime();
            FranjaReprogramada = $"{horaDesde:HH:mm}-{horaHasta:HH:mm}";
        }

        FechaEntregaComprometida = fecha;
        HoraEntregaDesde = horaDesde;
        HoraEntregaHasta = horaHasta;
    }

    public void ValidarBultosEntregaParcial(
        IReadOnlyCollection<Guid> bultosEntregados)
    {
        ArgumentNullException.ThrowIfNull(bultosEntregados);

        if (Estado != EstadoEnvio.EnTransito)
            throw new InvalidOperationException(
                "El envío debe estar en tránsito.");

        if (bultosEntregados.Count == 0)
            throw new ArgumentException(
                "Debe indicarse al menos un bulto entregado.");

        if (bultosEntregados.Count != bultosEntregados.Distinct().Count())
            throw new InvalidOperationException(
                "Hay bultos repetidos.");

        var pendientes = _bultos
            .Where(b => b.Estado == EstadoBulto.Pendiente)
            .ToList();

        if (bultosEntregados.Count >= pendientes.Count)
            throw new InvalidOperationException(
                "Una entrega parcial debe dejar al menos un bulto pendiente.");

        var idsPendientes = pendientes
            .Select(b => b.Id)
            .ToHashSet();

        if (bultosEntregados.Any(id => !idsPendientes.Contains(id)))
            throw new InvalidOperationException(
                "Todos los bultos indicados deben pertenecer al envío y estar pendientes.");
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
        decimal? longitud = null,
        IReadOnlyCollection<Guid>? bultosEntregados = null,   
        IReadOnlyCollection<(Guid BultoId, MotivoNoEntrega Motivo, string Evidencia)>?
            bultosNoEntregados = null,
        ModalidadDevolucion? modalidadDevolucion = null,
        string? receptorDevolucion = null,
        string? referenciaConstanciaDevolucion = null)

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
            nuevoEstado => Estado = nuevoEstado,
            ObtenerEstadoAnteriorAsignacion);

        if (!maquina.CanFire(disparador))
        {
            throw new InvalidOperationException(
                $"No se puede aplicar {disparador} desde el estado {Estado}.");
        }

        IntentoEntrega? nuevoIntento = null;
        List<Bulto> bultosParaEntregar = [];
        switch (disparador)
        {
            case DisparadorEnvio.AsignarARuta:
                if (Estado == EstadoEnvio.EnDeposito &&
                    IntentosRealizados != 0)
                {
                    throw new InvalidOperationException(
                        "Después de un intento fallido debe reprogramarse.");
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

                case DisparadorEnvio.RegistrarEntregaParcial:
                {
                    if (IntentosRealizados >= reglas.MaximoIntentosEntrega)
                        throw new InvalidOperationException(
                            "Se alcanzó el máximo de intentos.");

                    if (pruebaEntrega is null)
                        throw new InvalidOperationException(
                            "La entrega parcial requiere una prueba de entrega.");

                    if (reglas.RequierePruebaEntrega &&
                        string.IsNullOrWhiteSpace(pruebaEntrega.ReferenciaFirma) &&
                        string.IsNullOrWhiteSpace(pruebaEntrega.ReferenciaFotografia))
                    {
                        throw new InvalidOperationException(
                            "La prueba de entrega no cumple las reglas del operador.");
                    }

                    if (bultosEntregados is null || bultosNoEntregados is null)
                        throw new ArgumentException(
                            "Deben indicarse los resultados de todos los bultos pendientes.");

                    ValidarBultosEntregaParcial(bultosEntregados);

                    if (bultosNoEntregados.Count == 0)
                        throw new InvalidOperationException(
                            "Una entrega parcial debe incluir bultos no entregados.");

                    var pendientes = _bultos
                        .Where(b => b.Estado == EstadoBulto.Pendiente)
                        .Select(b => b.Id)
                        .ToHashSet();

                    var idsNoEntregados = bultosNoEntregados
                        .Select(b => b.BultoId)
                        .ToList();

                    if (idsNoEntregados.Count != idsNoEntregados.Distinct().Count())
                        throw new InvalidOperationException(
                            "Hay bultos no entregados repetidos.");

                    var idsInformados = bultosEntregados
                        .Concat(idsNoEntregados)
                        .ToHashSet();

                    if (!idsInformados.SetEquals(pendientes))
                        throw new InvalidOperationException(
                            "Debe informarse el resultado de todos los bultos pendientes.");

                    foreach (var resultado in bultosNoEntregados)
                    {
                        if (resultado.Motivo is null ||
                            resultado.Motivo.OperadorId != OperadorId ||
                            resultado.Motivo.VersionReglasOperativasId != reglas.Id)
                        {
                            throw new InvalidOperationException(
                                "Uno de los motivos no pertenece al catálogo del envío.");
                        }

                        if (string.IsNullOrWhiteSpace(resultado.Evidencia))
                            throw new InvalidOperationException(
                                "Cada bulto no entregado requiere evidencia del fallo.");
                    }

                    nuevoIntento = IntentoEntrega.RegistrarEntregaParcial(
                        OperadorId,
                        Id,
                        IntentosRealizados + 1,
                        bultosEntregados,
                        bultosNoEntregados
                            .Select(b => (
                                b.BultoId,
                                b.Motivo.Id,
                                b.Evidencia))
                            .ToList(),
                        pruebaEntrega);

                    bultosParaEntregar = _bultos
                        .Where(b => bultosEntregados.Contains(b.Id))
                        .ToList();

                    break;
                }

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
                // Se permite registrar el regreso físico al depósito.
                // La decisión de devolverlo al comercio se realiza después.
                break;
                  
            case DisparadorEnvio.IniciarDevolucion:
                {
                    var motivoBloqueo = ObtenerMotivoBloqueoDevolucion(reglas);

                    if (motivoBloqueo is not null)
                    {
                        throw new InvalidOperationException(motivoBloqueo);
                    }

                    break;
                }
 
            case DisparadorEnvio.ConfirmarDevolucion:
                if (!_bultos.Any(b => b.Estado == EstadoBulto.EnDevolucion))
                {
                    throw new InvalidOperationException(
                        "El envío no tiene bultos en devolución.");
                }

                if (!modalidadDevolucion.HasValue ||
                    !Enum.IsDefined(modalidadDevolucion.Value))
                {
                    throw new ArgumentException(
                        "Debe indicar una modalidad de devolución válida.");
                }

                if (string.IsNullOrWhiteSpace(receptorDevolucion) ||
                    string.IsNullOrWhiteSpace(referenciaConstanciaDevolucion))
                {
                    throw new ArgumentException(
                        "Debe registrar el receptor y la constancia de recepción.");
                }

                break;

            case DisparadorEnvio.Reprogramar:
                if (IntentosRealizados == 0)
                    throw new InvalidOperationException(
                        "La reprogramación requiere un intento fallido previo.");

                if (IntentosRealizados >= reglas.MaximoIntentosEntrega ||
                    UltimoMotivoRequiereDevolucion)
                {
                    throw new InvalidOperationException(
                        "El envío no admite nuevos intentos de entrega.");
                }

                break;

            case DisparadorEnvio.DeclararExtraviado:
                if (string.IsNullOrWhiteSpace(justificacion))
                    throw new ArgumentException(
                        "Debe justificarse la declaración de extravío.");

                if (justificacion!.Trim().Length > 1000)
                    throw new ArgumentException(
                        "La justificación no puede superar los 1000 caracteres.");
                break;

            case DisparadorEnvio.QuitarDeRuta:
                if (ObtenerEstadoAnteriorAsignacion() == EstadoEnvio.Reprogramado)
                {
                    FechaReprogramada = null;
                    FranjaReprogramada = null;
                    FechaEntregaComprometida = null;
                    HoraEntregaDesde = null;
                    HoraEntregaHasta = null;
                }
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
            longitud,      
            disparador == DisparadorEnvio.DeclararExtraviado
                ? justificacion
                : null,
            modalidadDevolucion: modalidadDevolucion,
            receptorDevolucion: receptorDevolucion,
            referenciaConstanciaDevolucion: referenciaConstanciaDevolucion);

        maquina.Fire(disparador);
        if (disparador == DisparadorEnvio.RegistrarEntregaParcial)
        {
            foreach (var bulto in bultosParaEntregar)
            {
                bulto.Aplicar(
                    MaquinaEstadosBulto.Disparador.Entregar);
            }
        }
        else if (disparador == DisparadorEnvio.Entregar)
        {
            foreach (var bulto in _bultos.Where(
                b => b.Estado == EstadoBulto.Pendiente))
            {
                bulto.Aplicar(
                    MaquinaEstadosBulto.Disparador.Entregar);
            }
        }

        else if (disparador == DisparadorEnvio.IniciarDevolucion)
        {
            foreach (var bulto in _bultos.Where(
                b => b.Estado == EstadoBulto.Pendiente))
            {
                bulto.Aplicar(
                    MaquinaEstadosBulto.Disparador.IniciarDevolucion);
            }
        }
        else if (disparador == DisparadorEnvio.ConfirmarDevolucion)
        {
            foreach (var bulto in _bultos.Where(
                b => b.Estado == EstadoBulto.EnDevolucion))
            {
                bulto.Aplicar(
                    MaquinaEstadosBulto.Disparador.ConfirmarDevolucion);
            }
        }

        if (nuevoIntento is not null)
        {
            _intentos.Add(nuevoIntento);
            IntentosRealizados++;

            if (disparador == DisparadorEnvio.RegistrarEntregaParcial)
            {
                var motivos = bultosNoEntregados!
                    .Select(b => b.Motivo)
                    .ToList();

                UltimoMotivoNoEntregaId =
                    motivos.Select(m => m.Id).Distinct().Count() == 1
                        ? motivos[0].Id
                        : null;

                UltimoMotivoRequiereDevolucion =
                    motivos.Any(m => m.RequiereDevolucion);
            }
            else if (!nuevoIntento.Entregado)
            {
                UltimoMotivoNoEntregaId = motivoNoEntrega!.Id;
                UltimoMotivoRequiereDevolucion =
                    motivoNoEntrega.RequiereDevolucion;
            }
        }

        if (disparador == DisparadorEnvio.Reprogramar)
        {
            FechaReprogramada = null;
            FranjaReprogramada = null;

            // Se elimina el compromiso anterior.
            // La nueva fecha se establecerá al planificar otra ruta.
            FechaEntregaComprometida = null;
            HoraEntregaDesde = null;
            HoraEntregaHasta = null;
        }
        _eventos.Add(evento);
    }

public bool PuedeIniciarDevolucion(
    VersionReglasOperativas reglas)
{
    ArgumentNullException.ThrowIfNull(reglas);

    return ObtenerMotivoBloqueoDevolucion(reglas) is null;
}

private string? ObtenerMotivoBloqueoDevolucion(
    VersionReglasOperativas reglas)
{
    if (VersionReglasOperativasId != reglas.Id ||
        reglas.OperadorId != OperadorId)
    {
        return "Las reglas no corresponden a este envío.";
    }

    if (Estado != EstadoEnvio.EnDeposito)
    {
        return "El envío debe estar en depósito para iniciar su devolución.";
    }

    if (!_bultos.Any(b => b.Estado == EstadoBulto.Pendiente))
    {
        return "El envío no tiene bultos pendientes para devolver.";
    }

    if (IntentosRealizados == 0)
    {
        return "La devolución requiere al menos un intento de entrega.";
    }

    if (IntentosRealizados < reglas.MaximoIntentosEntrega &&
        !UltimoMotivoRequiereDevolucion &&
        reglas.AccionBultosPendientes !=
            AccionBultosPendientes.DevolverAlComercio)
    {
        return "Las reglas operativas indican que corresponde una nueva visita.";
    }

    return null;
}
    private EstadoEnvio ObtenerEstadoDestino(DisparadorEnvio disparador)
    {
        return disparador switch
        {
            DisparadorEnvio.RecibirEnDeposito => EstadoEnvio.EnDeposito,
            DisparadorEnvio.AsignarARuta => EstadoEnvio.AsignadoARuta,
            DisparadorEnvio.QuitarDeRuta => ObtenerEstadoAnteriorAsignacion(),
            DisparadorEnvio.Despachar => EstadoEnvio.EnTransito,
            DisparadorEnvio.Entregar => EstadoEnvio.Entregado,
            DisparadorEnvio.RegistrarEntregaParcial => EstadoEnvio.EntregadoParcialmente,
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

    private EstadoEnvio ObtenerEstadoAnteriorAsignacion()
    {
        var asignacion = _eventos
            .LastOrDefault(e =>
                e.Disparador == DisparadorEnvio.AsignarARuta);

        if (asignacion is null)
            throw new InvalidOperationException(
                "No existe una asignación previa registrada.");

        if (asignacion.EstadoAnterior != EstadoEnvio.EnDeposito &&
            asignacion.EstadoAnterior != EstadoEnvio.Reprogramado)
        {
            throw new InvalidOperationException(
                "El estado anterior a la asignación no es válido.");
        }

        return asignacion.EstadoAnterior;
    }
}
