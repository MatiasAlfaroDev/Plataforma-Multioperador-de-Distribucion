namespace UltimaMilla.Application.Envios.Commands.RegistrarReaparicionBulto;

public sealed record RegistrarReaparicionBultoCommand(
    Guid OperadorId,
    Guid EnvioId,
    string Identificacion,
    string Responsable);
