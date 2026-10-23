namespace UltimaMilla.Mobile.Models;

/// <summary>Lo que la app manda a POST /api/auth/login.</summary>
public sealed record LoginRequest(
    string Email,
    string Password,
    string TipoContexto);

public sealed record UsuarioLogin(
    Guid Id,
    string Email,
    string NombreCompleto,
    Guid? OperadorId,
    string Rol);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt,
    UsuarioLogin Usuario);