namespace UltimaMilla.Application.Envios.Commands.DeclararExtravio;

public sealed record DeclararExtravioCommand(
    Guid OperadorId,
    Guid EnvioId,
    string Justificacion,
    string Responsable);