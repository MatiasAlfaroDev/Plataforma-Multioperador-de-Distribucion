using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Envios;

/// <summary>
/// Un hecho que el repartidor registró en la app, muchas veces sin conexión, y que llega al
/// servidor al sincronizar (RF 18, 21 y 24): "entregué el envío" o "no lo pude entregar".
/// </summary>
/// <remarks>
/// El Id NO lo genera el servidor: es el que generó el teléfono al registrar el hecho.
/// Como es la clave primaria, el mismo evento no puede guardarse dos veces aunque la app
/// lo reintente: eso es lo que hace idempotente la sincronización.
/// </remarks>
public class EventoMovil : AuditableEntity, ITenantEntity
{
    /// <summary>Tolerancia para relojes de teléfono un poco adelantados.</summary>
    private static readonly TimeSpan ToleranciaReloj = TimeSpan.FromMinutes(5);

    public Guid OperadorId { get; private set; }
    public Guid EnvioId { get; private set; }
    public TipoEventoMovil Tipo { get; private set; }

    /// <summary>Motivo del catálogo, solo para NoEntregado.</summary>
    public string? Motivo { get; private set; }

    /// <summary>Nombre de quien recibió, solo para Entregado.</summary>
    public string? Receptor { get; private set; }

    /// <summary>Cuándo pasó en la calle (hora del teléfono).</summary>
    public DateTimeOffset OcurridoEn { get; private set; }

    /// <summary>Cuándo llegó al servidor. La diferencia con OcurridoEn es el tiempo que estuvo sin conexión.</summary>
    public DateTimeOffset RecibidoEn { get; private set; }

    // Constructor vacío que necesita EF Core para materializar desde la base.
    private EventoMovil() { }

    public static EventoMovil Registrar(
        Guid idEvento,
        Envio envio,
        TipoEventoMovil tipo,
        string? motivo,
        string? receptor,
        DateTimeOffset ocurridoEn,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(envio);

        if (idEvento == Guid.Empty)
            throw new ArgumentException("El evento debe traer el Id generado en el teléfono.", nameof(idEvento));
        if (tipo == TipoEventoMovil.Entregado && string.IsNullOrWhiteSpace(receptor))
            throw new ArgumentException("Una entrega debe indicar quién recibió el paquete.", nameof(receptor));
        if (tipo == TipoEventoMovil.NoEntregado && string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Un intento fallido debe indicar el motivo.", nameof(motivo));
        if (ocurridoEn > ahora + ToleranciaReloj)
            throw new ArgumentException("La fecha del evento está en el futuro.", nameof(ocurridoEn));
        if (ocurridoEn < envio.FechaAlta - ToleranciaReloj)
            throw new ArgumentException("La fecha del evento es anterior al alta del envío.", nameof(ocurridoEn));

        return new EventoMovil
        {
            Id = idEvento,
            OperadorId = envio.OperadorId,
            EnvioId = envio.Id,
            Tipo = tipo,
            Motivo = tipo == TipoEventoMovil.NoEntregado ? motivo!.Trim() : null,
            Receptor = tipo == TipoEventoMovil.Entregado ? receptor!.Trim() : null,
            // PostgreSQL (timestamptz) solo acepta fechas en UTC; el teléfono puede mandar otra zona.
            OcurridoEn = ocurridoEn.ToUniversalTime(),
            RecibidoEn = ahora.ToUniversalTime()
        };
    }
}

public enum TipoEventoMovil
{
    Entregado,
    NoEntregado
}
