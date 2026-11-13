namespace UltimaMilla.Domain.Envios;

/// <summary>
/// Historial por bulto (RF 31): cada cambio de estado de un bulto y cada bulto no entregado en una visita.
/// Lo deja Envio.Aplicar junto al EventoEnvio de la transición; EventosDeEstadoInterceptor lo publica
/// como EstadoBultoCambiado para los avisos v2 (RF 36).
/// </summary>
public sealed class EventoBulto
{
    public Guid Id { get; private set; }
    public Guid OperadorId { get; private set; }
    public Guid EnvioId { get; private set; }
    public Guid BultoId { get; private set; }
    public string Identificacion { get; private set; } = string.Empty;
    public EstadoBulto EstadoAnterior { get; private set; }
    public EstadoBulto EstadoNuevo { get; private set; }
    public ResultadoBulto Resultado { get; private set; }
    public string? Motivo { get; private set; }
    public DateTimeOffset OcurridoEn { get; private set; }

    private EventoBulto() { }

    internal static EventoBulto Crear(
        Envio envio,
        Bulto bulto,
        EstadoBulto estadoAnterior,
        ResultadoBulto resultado,
        string? motivo,
        DateTimeOffset ocurridoEn) => new()
        {
            Id = Guid.NewGuid(),
            OperadorId = envio.OperadorId,
            EnvioId = envio.Id,
            BultoId = bulto.Id,
            Identificacion = bulto.Identificacion,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = bulto.Estado,
            Resultado = resultado,
            Motivo = motivo,
            OcurridoEn = ocurridoEn
        };
}

/// <summary>Qué le pasó al bulto. NoEntregado no cambia su estado: sigue pendiente para otra visita.</summary>
public enum ResultadoBulto
{
    Entregado,
    NoEntregado,
    EnDevolucion,
    Devuelto,
    Extraviado,
    Reaparecido
}
