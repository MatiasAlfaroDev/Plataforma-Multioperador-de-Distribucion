
namespace UltimaMilla.Application.Auth.Commands.Register;

public enum RegisterStatus
{
    Exitoso,
    EmailYaRegistrado,
    RutYaRegistrado,
    DatosInvalidos
}

public sealed record RegisterResult(
    RegisterStatus Status,
    RegisterResponse? Response = null,
    IReadOnlyList<string>? Errores = null);
