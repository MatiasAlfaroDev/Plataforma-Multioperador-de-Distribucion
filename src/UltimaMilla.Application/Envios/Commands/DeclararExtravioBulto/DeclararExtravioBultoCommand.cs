namespace UltimaMilla.Application.Envios.Commands.DeclararExtravioBulto;

public sealed record DeclararExtravioBultoCommand(
    Guid OperadorId,
    Guid EnvioId,
    string Identificacion,
    string Justificacion,
    string Responsable);
