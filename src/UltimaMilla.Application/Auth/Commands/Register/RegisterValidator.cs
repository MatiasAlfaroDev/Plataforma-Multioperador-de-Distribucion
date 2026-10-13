
using FluentValidation;

namespace UltimaMilla.Application.Auth.Commands.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.NombreCompleto)
            .NotEmpty()
            .WithMessage("El nombre completo es obligatorio.")
            .MaximumLength(150)
            .WithMessage("El nombre no puede superar los 150 caracteres.");

        RuleFor(x => x.NombreComercio)
            .NotEmpty()
            .WithMessage("El nombre del comercio es obligatorio.")
            .MaximumLength(200)
            .WithMessage("El nombre del comercio no puede superar los 200 caracteres.");

        RuleFor(x => x.Rut)
            .NotEmpty()
            .WithMessage("El RUT es obligatorio.")
            .Matches(@"^\d{12}$")
            .WithMessage("El RUT debe contener exactamente 12 dígitos.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress()
            .WithMessage("El formato del correo electrónico no es válido.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .Matches("[A-Z]")
            .WithMessage("La contraseña debe contener una mayúscula.")
            .Matches("[a-z]")
            .WithMessage("La contraseña debe contener una minúscula.")
            .Matches("[0-9]")
            .WithMessage("La contraseña debe contener un número.");
    }
}
