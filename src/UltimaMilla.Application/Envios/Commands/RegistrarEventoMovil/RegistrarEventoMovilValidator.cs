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
            .WithMessage("El tipo debe ser Entregado, EntregadoParcialmente, NoEntregado o DevueltoAlComercio.");

        RuleFor(x => x.Receptor)
            .NotEmpty()
            .WithMessage("Una entrega debe indicar quién recibió los bultos.")
            .When(x =>
                EsTipo(x.Tipo, TipoEventoMovil.Entregado) ||
                EsTipo(x.Tipo, TipoEventoMovil.EntregadoParcialmente ) ||
                EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Un intento fallido debe indicar el motivo.")
            .When(x => EsTipo(x.Tipo, TipoEventoMovil.NoEntregado));

        RuleFor(x => x.Receptor).MaximumLength(200);
        RuleFor(x => x.Motivo).MaximumLength(200);
        RuleFor(x => x)
            .Must(x =>
            {
                bool esParcial =
                    EsTipo(x.Tipo, TipoEventoMovil.EntregadoParcialmente);

                bool tieneBultos =
                    x.BultosEntregados is not null ||
                    x.BultosNoEntregados is not null;

                if (!esParcial)
                    return !tieneBultos;

                return x.BultosEntregados is { Count: > 0 } &&
                    x.BultosNoEntregados is { Count: > 0 };
            })
            .WithMessage(
                "Solo una entrega parcial debe informar bultos entregados y no entregados.");

        RuleForEach(x => x.BultosEntregados)
            .NotEmpty();

        RuleForEach(x => x.BultosNoEntregados)
            .ChildRules(bulto =>
            {
                bulto.RuleFor(x => x.BultoId).NotEmpty();
                bulto.RuleFor(x => x.Motivo).NotEmpty();
                bulto.RuleFor(x => x.Evidencia).NotEmpty();
            });
    

        RuleFor(x => x.BultosDevueltos)
        .NotNull()
        .Must(b => b is { Count: > 0 })
        .WithMessage("La devolución debe incluir los bultos escaneados.")
        .When(x => EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));

    RuleForEach(x => x.BultosDevueltos)
        .NotEmpty();

    RuleFor(x => x)
        .Must(x => x.BultosDevueltos is null)
        .WithMessage("Solo una devolución puede informar bultos devueltos.")
        .When(x => !EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));

    RuleFor(x => x.DocumentoReceptor)
        .NotEmpty()
        .When(x => EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));

    RuleFor(x => x.ReferenciaFirma)
        .NotEmpty()
        .When(x => EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));

    RuleFor(x => x.ReferenciaFotografia)
        .NotEmpty()
        .When(x => EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));

    RuleFor(x => x.Latitud)
        .NotNull()
        .InclusiveBetween(-90m, 90m)
        .When(x => EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));

    RuleFor(x => x.Longitud)
        .NotNull()
        .InclusiveBetween(-180m, 180m)
        .When(x => EsTipo(x.Tipo, TipoEventoMovil.DevueltoAlComercio));
    }
    private static bool EsTipo(string? texto, TipoEventoMovil tipo) =>
        Enum.TryParse<TipoEventoMovil>(texto, ignoreCase: true, out var t) && t == tipo;
}
