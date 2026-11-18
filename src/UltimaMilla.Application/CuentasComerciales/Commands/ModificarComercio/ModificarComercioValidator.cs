using FluentValidation;

namespace UltimaMilla.Application.CuentasComerciales.Commands.ModificarComercio;

public sealed class ModificarComercioValidator : AbstractValidator<ModificarComercioCommand>
{
    public ModificarComercioValidator()
    {
        RuleFor(c => c.CuentaComercialId).NotEmpty();

        RuleFor(c => c.Nombre)
            .NotEmpty().WithMessage("El nombre del comercio es obligatorio.")
            .MaximumLength(200);
    }
}
