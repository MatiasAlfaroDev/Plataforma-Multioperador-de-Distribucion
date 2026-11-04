using FluentValidation;

namespace UltimaMilla.Application.Rutas.Commands.RegistrarPosiciones;

public sealed class RegistrarPosicionesValidator : AbstractValidator<RegistrarPosicionesCommand>
{
    /// <summary>La app manda de a lotes; un lote enorme indica un error (o un abuso).</summary>
    public const int MaximoPorLote = 500;

    public RegistrarPosicionesValidator()
    {
        RuleFor(c => c.Posiciones)
            .NotEmpty().WithMessage("El lote no tiene posiciones.")
            .Must(p => p.Count <= MaximoPorLote).WithMessage($"Un lote admite como máximo {MaximoPorLote} posiciones.");

        RuleForEach(c => c.Posiciones).ChildRules(p =>
        {
            p.RuleFor(x => x.Id).NotEmpty();
            p.RuleFor(x => x.Latitud).InclusiveBetween(-90, 90);
            p.RuleFor(x => x.Longitud).InclusiveBetween(-180, 180);
        });
    }
}
