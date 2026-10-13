namespace UltimaMilla.Application.Auth.Commands.Login;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt,
    UsuarioLoginDto Usuario);

public sealed record UsuarioLoginDto(
    Guid Id,
    string Email,
    string NombreCompleto,
    Guid? OperadorId,
    string Rol,
    string TipoContexto = "OPERADOR",
    Guid? ComercioId = null);