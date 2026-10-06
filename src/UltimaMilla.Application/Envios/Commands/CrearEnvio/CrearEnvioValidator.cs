using FluentValidation;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.CrearEnvio;

public sealed class CrearEnvioValidator : AbstractValidator<CrearEnvioCommand>
{
    public CrearEnvioValidator()
    {
        RuleFor(x => x.CuentaComercialId).NotEmpty();
        RuleFor(x => x.ReferenciaExterna).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Modalidad)
            .Must(m => Enum.TryParse<Modalidad>(m, ignoreCase: true, out _))
            .WithMessage("La modalidad debe ser Estandar o Urgente.");
        RuleFor(x => x.DestinatarioNombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DestinatarioTelefono).MaximumLength(30);
        RuleFor(x => x.Calle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Numero).MaximumLength(20);
        RuleFor(x => x.Localidad).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Departamento).NotEmpty().MaximumLength(50);

        RuleFor(x => x.Bultos).NotEmpty().WithMessage("Un envío debe tener al menos un bulto.");
        RuleForEach(x => x.Bultos).ChildRules(b =>
        {
            b.RuleFor(x => x.PesoKg).GreaterThan(0).LessThanOrEqualTo(1000);
            b.RuleFor(x => x.AltoCm).InclusiveBetween(1, 500);
            b.RuleFor(x => x.AnchoCm).InclusiveBetween(1, 500);
            b.RuleFor(x => x.ProfundidadCm).InclusiveBetween(1, 500);
        });
    }
}
