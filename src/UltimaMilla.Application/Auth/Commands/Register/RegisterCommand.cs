
namespace UltimaMilla.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string NombreCompleto,
    string NombreComercio,
    string Rut,
    string Email,
    string Password);
