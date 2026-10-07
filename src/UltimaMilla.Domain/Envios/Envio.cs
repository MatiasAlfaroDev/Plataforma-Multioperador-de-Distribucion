using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Domain.Envios;

/// <summary>
/// Agregado central. Cuelga de una CuentaComercial (no del Comercio) y lleva el OperadorId
/// de esa cuenta. Los setters son privados: el estado solo cambia con métodos del propio envío.
/// </summary>
public class Envio : AuditableEntity, ITenantEntity
{
    private readonly List<Bulto> _bultos = [];

    public Guid OperadorId { get; private set; }
    public Guid CuentaComercialId { get; private set; }
    public string ReferenciaExterna { get; private set; } = string.Empty;
    public Modalidad Modalidad { get; private set; }
    public EstadoEnvio Estado { get; private set; }
    public Destinatario Destinatario { get; private set; } = null!;
    public Direccion Direccion { get; private set; } = null!;
    public DateTimeOffset FechaAlta { get; private set; }
    public decimal? TarifaCalculada { get; private set; }
    public Guid? VersionTarifariaId { get; private set; }

    public IReadOnlyCollection<Bulto> Bultos => _bultos;

    public decimal PesoTotalKg => _bultos.Sum(b => b.PesoKg);
    public decimal VolumenTotalMetrosCubicos => _bultos.Sum(b => b.VolumenMetrosCubicos);

    // Constructor vacío que necesita EF Core para materializar desde la base.
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
            throw new InvalidOperationException("La cuenta comercial no está activa.");
        if (string.IsNullOrWhiteSpace(referenciaExterna))
            throw new ArgumentException("La referencia externa es obligatoria.", nameof(referenciaExterna));
        if (bultos is null || bultos.Count == 0)
            throw new ArgumentException("Un envío debe tener al menos un bulto.", nameof(bultos));

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
            envio._bultos.Add(Bulto.Crear($"{referencia}-{i + 1}", bultos[i]));

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
}
