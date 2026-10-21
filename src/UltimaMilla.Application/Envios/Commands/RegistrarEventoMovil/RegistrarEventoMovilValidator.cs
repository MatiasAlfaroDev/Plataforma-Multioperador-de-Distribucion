using FluentValidation;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;

public sealed class RegistrarEventoMovilValidator : AbstractValidator<RegistrarEventoMovilCommand>
{
    public RegistrarEventoMovilValidator()
    {
        RuleFor(x => x.EnvioId).NotEmpty();
        RuleFor(x => x.IdEvento).NotEmpty();
        RuleFor(x => x.FechaHora).NotEmpty();
        RuleFor(x => x.Tipo)
            .Must(t => Enum.TryParse<TipoEventoMovil>(t, ignoreCase: true, out _))
            .WithMessage("El tipo debe ser Entregado o NoEntregado.");

        RuleFor(x => x.Receptor)
            .NotEmpty().WithMessage("Una entrega debe indicar quién recibió el paquete.")
            .When(x => EsTipo(x.Tipo, TipoEventoMovil.Entregado));
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Un intento fallido debe indicar el motivo.")
            .When(x => EsTipo(x.Tipo, TipoEventoMovil.NoEntregado));

        RuleFor(x => x.Receptor).MaximumLength(200);
        RuleFor(x => x.Motivo).MaximumLength(200);
    }

    private static bool EsTipo(string? texto, TipoEventoMovil tipo) =>
        Enum.TryParse<TipoEventoMovil>(texto, ignoreCase: true, out var t) && t == tipo;
}
