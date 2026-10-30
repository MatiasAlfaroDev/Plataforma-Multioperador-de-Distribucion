namespace UltimaMilla.Application.Envios.Commands.Reprogramacion;

public sealed record ConfirmarReprogramacionCommand(
    Guid SolicitudId,
    string Responsable);