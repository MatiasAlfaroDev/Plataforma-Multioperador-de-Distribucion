
using UltimaMilla.Application.Auth.Commands.Register;

namespace UltimaMilla.Application.Abstractions;

public interface IRegistrationService
{
    Task<RegisterResult> RegisterAsync(
        RegisterCommand command,
        CancellationToken ct);
}
