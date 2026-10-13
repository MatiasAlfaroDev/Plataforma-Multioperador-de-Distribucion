
using UltimaMilla.Application.Auth.Commands.Login;

namespace UltimaMilla.Application.Abstractions;

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(
        LoginCommand command,
        CancellationToken ct);
}
