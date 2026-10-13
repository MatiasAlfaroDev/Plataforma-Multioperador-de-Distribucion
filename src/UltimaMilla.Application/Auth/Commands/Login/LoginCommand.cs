namespace UltimaMilla.Application.Auth.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    Guid? OperadorId,
    string TipoContexto = "OPERADOR",
    Guid? ComercioId = null);