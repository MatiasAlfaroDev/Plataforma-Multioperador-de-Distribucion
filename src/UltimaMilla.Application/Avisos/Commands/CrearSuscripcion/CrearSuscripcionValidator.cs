using FluentValidation;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.CrearSuscripcion;

public sealed class CrearSuscripcionValidator : AbstractValidator<CrearSuscripcionCommand>
{
    public CrearSuscripcionValidator()
    {
        RuleFor(c => c.Url)
            .NotEmpty().WithMessage("La URL es obligatoria.")
            .MaximumLength(SuscripcionAviso.LargoMaximoUrl);

        RuleFor(c => c.TiposEvento)
            .NotEmpty().WithMessage("Hay que elegir al menos un tipo de evento.");

        RuleForEach(c => c.TiposEvento)
            .Must(t => TiposAviso.Todos.Contains(t))
            .WithMessage("Tipo de evento desconocido.");
    }
}
