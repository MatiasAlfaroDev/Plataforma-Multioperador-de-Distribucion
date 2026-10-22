namespace UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;

public sealed record RecibirEnDepositoCommand(
    Guid EnvioId,
    IReadOnlyList<string> IdentificacionesEscaneadas,
    string Responsable);