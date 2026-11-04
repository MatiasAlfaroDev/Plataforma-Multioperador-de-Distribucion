namespace UltimaMilla.Application.Envios.Commands.RetornarADeposito;

public sealed record RetornarADepositoCommand(
    Guid EnvioId,
    IReadOnlyList<string> IdentificacionesEscaneadas,
    string Responsable);