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

    /// <summary>"Entregado", "EntregadoParcialmente" o "NoEntregado"; en un retiro, "Retirado" o "NoRetirado".</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Motivo del catálogo, solo para "NoEntregado".</summary>
    public string? Motivo { get; set; }

    /// <summary>Nombre de quien recibió, solo para "Entregado".</summary>
    public string? Receptor { get; set; }

    /// <summary>Momento real en que ocurrió en la calle, no el momento en que se sincronizó.</summary>
    public DateTimeOffset FechaHora { get; set; }

    /// <summary>Posición del dispositivo al registrar (obligatoria para Entregado).</summary>
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }

    /// <summary>Referencia de la foto: prueba de entrega (Entregado) o evidencia (NoEntregado).</summary>
    public string? ReferenciaFoto { get; set; }

    /// <summary>Documento de quien recibió, solo para "Entregado" (RF 20).</summary>
    public string? DocumentoReceptor { get; set; }

    /// <summary>Referencia de la firma en pantalla (archivo SVG), solo para "Entregado" (RF 20).</summary>
    public string? ReferenciaFirma { get; set; }

    /// <summary>Entrega parcial (RF 32): Ids de los bultos que se entregaron.</summary>
    public IReadOnlyList<Guid>? BultosEntregados { get; set; }

    /// <summary>Entrega parcial (RF 32): los bultos que no se entregaron, cada uno con motivo y evidencia.</summary>
    public IReadOnlyList<BultoNoEntregado>? BultosNoEntregados { get; set; }

    /// <summary>Retiro (RF 40): identificaciones de los bultos escaneados en el domicilio.</summary>
    public IReadOnlyList<string>? BultosRetirados { get; set; }

    /// <summary>"Retirado" o "NoRetirado": el evento es de un retiro y EnvioId es el Id del retiro.</summary>
    public bool EsRetiro => Tipo is "Retirado" or "NoRetirado";

    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
}
