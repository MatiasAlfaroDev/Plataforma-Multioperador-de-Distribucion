using FluentValidation;

namespace UltimaMilla.Application.Retiros.Commands.CrearRetiro;

public sealed class CrearRetiroValidator
    : AbstractValidator<CrearRetiroCommand>
{
    public CrearRetiroValidator()
    {
        RuleFor(x => x.CuentaComercialId).NotEmpty();

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.DomicilioRetiro)
            .NotNull();

        RuleFor(x => x.DomicilioDestino)
            .NotNull();

        RuleFor(x => x.DomicilioRetiro)
            .SetValidator(new DireccionRetiroValidator()!)
            .When(x => x.DomicilioRetiro is not null);

        RuleFor(x => x.DomicilioDestino)
            .SetValidator(new DireccionRetiroValidator()!)
            .When(x => x.DomicilioDestino is not null);

        RuleFor(x => x.Fecha)
            .NotEmpty();

        RuleFor(x => x.HoraDesde)
            .LessThan(x => x.HoraHasta);

        RuleFor(x => x.Bultos)
            .NotEmpty()
            .WithMessage("Debe indicar al menos un bulto.");

        RuleForEach(x => x.Bultos)
            .SetValidator(new BultoRetiroValidator());

        RuleFor(x => x.Bultos)
            .Must(bultos =>
                bultos is not null &&
                bultos.All(b => b is not null) &&
                bultos.Select(b => b.Identificacion?.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() == bultos.Count)
            .When(x => x.Bultos is { Count: > 0 })
            .WithMessage("No se permiten identificaciones de bultos repetidas.");
    }
}

public sealed class DireccionRetiroValidator
    : AbstractValidator<DireccionRetiroCommand>
{
    public DireccionRetiroValidator()
    {
        RuleFor(x => x.Calle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Numero).MaximumLength(20);
        RuleFor(x => x.Localidad).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Departamento).NotEmpty().MaximumLength(50);
    }
}

public sealed class BultoRetiroValidator
    : AbstractValidator<BultoRetiroCommand>
{
    public BultoRetiroValidator()
    {
        RuleFor(x => x.Identificacion).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PesoKg).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.AltoCm).InclusiveBetween(1, 500);
        RuleFor(x => x.AnchoCm).InclusiveBetween(1, 500);
        RuleFor(x => x.ProfundidadCm).InclusiveBetween(1, 500);
    }
}