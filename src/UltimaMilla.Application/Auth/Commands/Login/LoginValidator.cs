using FluentValidation;

namespace UltimaMilla.Application.Auth.Commands.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress()
            .WithMessage("El formato del correo electrónico no es válido.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("La contraseña es obligatoria.");

        // Validar el tipo de contexto.
        RuleFor(x => x.TipoContexto)
            .Must(tipo =>
                tipo is not null &&
                (tipo.Trim().Equals("OPERADOR", StringComparison.OrdinalIgnoreCase)
                 || tipo.Trim().Equals("COMERCIO", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("El tipo de contexto debe ser OPERADOR o COMERCIO.");

        // Si ingresa como operador, no puede seleccionar un comercio.
        When(x => string.Equals(
            x.TipoContexto?.Trim(),
            "OPERADOR",
            StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.ComercioId)
                .Null()
                .WithMessage(
                    "No se puede indicar ComercioId al ingresar como operador.");

            RuleFor(x => x.OperadorId)
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithMessage("El OperadorId no puede ser un GUID vacío.");
        });

        // Si ingresa como comercio, no puede seleccionar un operador.
        When(x => string.Equals(
            x.TipoContexto?.Trim(),
            "COMERCIO",
            StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.OperadorId)
                .Null()
                .WithMessage(
                    "No se puede indicar OperadorId al ingresar como comercio.");

            RuleFor(x => x.ComercioId)
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithMessage("El ComercioId no puede ser un GUID vacío.");
        });
    }
}