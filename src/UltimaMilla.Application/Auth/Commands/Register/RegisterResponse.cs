
namespace UltimaMilla.Application.Auth.Commands.Register;

public sealed record RegisterResponse(
    Guid UsuarioId,
    Guid ComercioId,
    string NombreComercio,
    string Email);
