namespace UltimaMilla.Application.Envios.Eventos;

/// <summary>
/// Evento de negocio que publica la sincronización de la app del repartidor por el outbox:
/// el envío se entregó o tuvo un intento fallido. Lo consumirá el Worker (aviso al comercio, RF 25).
/// </summary>
/// <remarks>
/// MassTransit nombra el exchange por namespace y nombre del tipo: no moverlo una vez que el Worker lo consuma.
/// </remarks>
public sealed record EventoMovilRegistrado(
    Guid EventoId,
    Guid EnvioId,
    Guid OperadorId,
    string Tipo,
    DateTimeOffset OcurridoEn);
