namespace UltimaMilla.Mobile.Models;

/// <summary>
/// Algo que el repartidor hizo sin conexión y que todavía hay que mandar al servidor
/// (tabla eventos_pendientes). Funciona como una cola: se envían en orden y se borran
/// solo cuando el servidor confirma.
/// </summary>
public sealed class EventoPendiente
{
    /// <summary>
    /// Lo genera el teléfono al registrar el evento y viaja al servidor. Si el mismo evento
    /// llega dos veces (por ejemplo, se cortó la conexión antes de recibir la respuesta),
    /// el servidor lo reconoce por este Id y no lo procesa de nuevo: eso es la idempotencia.
    /// </summary>
    public Guid Id { get; set; }

    public Guid EnvioId { get; set; }

    /// <summary>"Entregado" o "NoEntregado".</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Motivo del catálogo, solo para "NoEntregado".</summary>
    public string? Motivo { get; set; }

    /// <summary>Nombre de quien recibió, solo para "Entregado".</summary>
    public string? Receptor { get; set; }

    /// <summary>Momento real en que ocurrió en la calle, no el momento en que se sincronizó.</summary>
    public DateTimeOffset FechaHora { get; set; }

    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
}
