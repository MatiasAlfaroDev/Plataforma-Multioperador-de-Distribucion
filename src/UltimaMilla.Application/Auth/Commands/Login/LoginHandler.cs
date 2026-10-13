
using FluentValidation;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Auth.Commands.Login;

public sealed class LoginHandler(
    IAuthenticationService authenticationService,
    IValidator<LoginCommand> validator)
{
    public async Task<LoginResult> Handle(
        LoginCommand command,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        return await authenticationService.LoginAsync(command, ct);
    }
}
