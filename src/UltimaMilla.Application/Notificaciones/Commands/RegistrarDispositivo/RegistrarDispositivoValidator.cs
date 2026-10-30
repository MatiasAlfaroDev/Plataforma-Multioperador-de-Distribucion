using FluentValidation;
using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.Application.Notificaciones.Commands.RegistrarDispositivo;

public sealed class RegistrarDispositivoValidator : AbstractValidator<RegistrarDispositivoCommand>
{
    private static readonly string[] Plataformas = ["Android", "iOS"];

    public RegistrarDispositivoValidator()
    {
        RuleFor(c => c.Token)
            .NotEmpty().WithMessage("El token del dispositivo es obligatorio.")
            .MaximumLength(DispositivoMovil.LargoMaximoToken);

        RuleFor(c => c.Plataforma)
            .Must(p => Plataformas.Contains(p))
            .WithMessage("La plataforma debe ser Android o iOS.");
    }
}
