namespace UltimaMilla.Application.Envios.Eventos;

/// <summary>
/// Evento de negocio que publica el alta de envío (RF 7) por el outbox; lo consumirá el Worker.
/// </summary>
/// <remarks>
/// MassTransit nombra el exchange por namespace y nombre del tipo, así que si se mueve a UltimaMilla.Contracts
/// tiene que ser antes de que el Worker lo consuma.
/// </remarks>
public sealed record EnvioCreado(
    Guid EnvioId,
    Guid OperadorId,
    Guid CuentaComercialId,
    string ReferenciaExterna,
    DateTimeOffset FechaAlta);
