namespace UltimaMilla.Application.Avisos.Eventos;

/// <summary>
/// Evento publicado cuando se crea una nueva entrega de aviso pendiente.
/// </summary>
/// <remarks>
/// Lo consume el Worker para enviar los avisos. El OperadorId viaja en el mensaje para fijar el inquilino (informe 5.1).
/// MassTransit nombra el exchange por namespace y nombre del tipo: no moverlo.
/// </remarks>
public sealed record AvisoPendiente(
    Guid EntregaAvisoId,
    Guid OperadorId);
