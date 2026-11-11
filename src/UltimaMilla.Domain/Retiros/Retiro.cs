using Stateless;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Domain.Retiros;

public sealed class Retiro : AuditableEntity, ITenantEntity
{
    private readonly List<BultoRetiro> _bultos = [];

    public Guid OperadorId { get; private set; }
    public Guid CuentaComercialId { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public Direccion DomicilioRetiro { get; private set; } = null!;
    public Direccion DomicilioDestino { get; private set; } = null!;

    public DateOnly FechaSolicitada { get; private set; }
    public TimeOnly HoraDesde { get; private set; }
    public TimeOnly HoraHasta { get; private set; }

    public EstadoRetiro Estado { get; private set; }

    public DateTimeOffset SolicitadoEn { get; private set; }

    public Guid? HojaDeRutaId { get; private set; }

    public string? ReferenciaFotografia { get; private set; }
    public string? ReferenciaFirma { get; private set; }
    public DateTimeOffset? RetiradoEn { get; private set; }

    public IReadOnlyCollection<BultoRetiro> Bultos => _bultos;

    public decimal PesoTotalKg => _bultos.Sum(b => b.PesoKg);

    public decimal VolumenTotalMetrosCubicos =>
        _bultos.Sum(b => b.VolumenMetrosCubicos);

    public Guid? VersionTarifariaId { get; private set; }
    public decimal? TarifaCalculada { get; private set; }

    private Retiro() { }

    public static Retiro Crear(
        Guid operadorId,
        Guid cuentaComercialId,
        string motivo,
        Direccion domicilioRetiro,
        Direccion domicilioDestino,
        DateOnly fecha,
        TimeOnly horaDesde,
        TimeOnly horaHasta,
        IReadOnlyCollection<BultoRetiro> bultos,
        DateTimeOffset ahora)
    {
        if (operadorId == Guid.Empty ||
            cuentaComercialId == Guid.Empty)
            throw new ArgumentException(
                "El operador y la cuenta comercial son obligatorios.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException(
                "El motivo del retiro es obligatorio.");

        if (motivo.Trim().Length > 500)
            throw new ArgumentException(
                "El motivo no puede superar los 500 caracteres.");

        ArgumentNullException.ThrowIfNull(domicilioRetiro);
        ArgumentNullException.ThrowIfNull(domicilioDestino);
        ArgumentNullException.ThrowIfNull(bultos);

        if (horaDesde >= horaHasta)
            throw new ArgumentException(
                "La franja horaria no es válida.");

        if (fecha < DateOnly.FromDateTime(
                ahora.ToOffset(TimeSpan.FromHours(-3)).DateTime))
            throw new ArgumentException(
                "La fecha solicitada no puede ser anterior a hoy.");

        if (bultos.Count == 0)
            throw new ArgumentException(
                "Debe indicar al menos un bulto.");

        if (bultos.Any(b => b is null))
            throw new ArgumentException(
                "La lista contiene bultos inválidos.");

        if (bultos.Select(b => b.Identificacion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != bultos.Count)
            throw new ArgumentException(
                "Hay identificaciones de bultos repetidas.");

        var retiro = new Retiro
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            CuentaComercialId = cuentaComercialId,
            Motivo = motivo.Trim(),
            DomicilioRetiro = domicilioRetiro,
            DomicilioDestino = domicilioDestino,
            FechaSolicitada = fecha,
            HoraDesde = horaDesde,
            HoraHasta = horaHasta,
            Estado = EstadoRetiro.Solicitado,
            SolicitadoEn = ahora
        };

        retiro._bultos.AddRange(bultos);

        return retiro;
    }

    public void AsignarARuta(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            throw new ArgumentException(
                "La hoja de ruta es obligatoria.");

        var maquina = CrearMaquina();
        maquina.Fire(DisparadorRetiro.AsignarARuta);

        HojaDeRutaId = hojaDeRutaId;
    }

    public void RegistrarRetiro(
        DateTimeOffset fecha,
        string referenciaFotografia,
        string referenciaFirma)
    {
        if (string.IsNullOrWhiteSpace(referenciaFotografia) ||
            string.IsNullOrWhiteSpace(referenciaFirma))
            throw new ArgumentException(
                "El retiro requiere fotografía y firma.");

        var maquina = CrearMaquina();
        maquina.Fire(DisparadorRetiro.RegistrarRetiro);

        ReferenciaFotografia = referenciaFotografia.Trim();
        ReferenciaFirma = referenciaFirma.Trim();
        RetiradoEn = fecha;
    }

    public void RegistrarRetiroFallido()
    {
        CrearMaquina().Fire(DisparadorRetiro.RegistrarRetiroFallido);
    }

    public void Reprogramar()
    {
        CrearMaquina().Fire(DisparadorRetiro.Reprogramar);
    }

    public void RecibirEnDeposito()
    {
        CrearMaquina().Fire(DisparadorRetiro.RecibirEnDeposito);
    }

    public void EntregarAlComercio()
    {
        CrearMaquina().Fire(DisparadorRetiro.EntregarAlComercio);
    }

    private StateMachine<EstadoRetiro, DisparadorRetiro> CrearMaquina()
    {
        var maquina = new StateMachine<EstadoRetiro, DisparadorRetiro>(
            () => Estado,
            nuevoEstado => Estado = nuevoEstado);

        maquina.Configure(EstadoRetiro.Solicitado)
            .Permit(
                DisparadorRetiro.AsignarARuta,
                EstadoRetiro.AsignadoARuta);

        maquina.Configure(EstadoRetiro.AsignadoARuta)
            .Permit(
                DisparadorRetiro.RegistrarRetiro,
                EstadoRetiro.Retirado)
            .Permit(
                DisparadorRetiro.RegistrarRetiroFallido,
                EstadoRetiro.NoRetirado);

        maquina.Configure(EstadoRetiro.NoRetirado)
            .Permit(
                DisparadorRetiro.Reprogramar,
                EstadoRetiro.Solicitado);

        maquina.Configure(EstadoRetiro.Retirado)
            .Permit(
                DisparadorRetiro.RecibirEnDeposito,
                EstadoRetiro.EnDeposito);

        maquina.Configure(EstadoRetiro.EnDeposito)
            .Permit(
                DisparadorRetiro.EntregarAlComercio,
                EstadoRetiro.EntregadoAlComercio);

        return maquina;
    }

    public void AplicarTarifa(
        Guid versionTarifariaId,
        decimal tarifaCalculada)
    {
        if (versionTarifariaId == Guid.Empty)
            throw new ArgumentException(
                "La versión tarifaria es obligatoria.");

        if (tarifaCalculada < 0)
            throw new ArgumentOutOfRangeException(
                nameof(tarifaCalculada));

        if (VersionTarifariaId.HasValue)
            throw new InvalidOperationException(
                "El retiro ya tiene una tarifa asignada.");

        VersionTarifariaId = versionTarifariaId;
        TarifaCalculada = tarifaCalculada;
    }
}