
using FluentValidation;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Auth.Commands.Register;

public sealed class RegisterHandler(
    IRegistrationService registrationService,
    IValidator<RegisterCommand> validator)
{
    public async Task<RegisterResult> Handle(
        RegisterCommand command,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        return await registrationService.RegisterAsync(command, ct);
    }
}
