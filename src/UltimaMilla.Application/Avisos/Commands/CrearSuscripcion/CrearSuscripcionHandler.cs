using FluentValidation;
using UltimaMilla.Application.Seguridad;
using System.Security.Cryptography;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.CrearSuscripcion;

/// <summary>
/// Alta de una suscripción a avisos (RNF 6.9): la hace el comercio para su cuenta o el administrador del operador para cualquiera de las suyas.
/// </summary>
public sealed class CrearSuscripcionHandler(
    ISuscripcionAvisoRepository suscripciones,
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext sesion,
    IValidator<CrearSuscripcionCommand> validator)
{
    public async Task<Guid> Handle(CrearSuscripcionCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var (operadorId, cuentaSesion) = AlcanceAvisos.Resolver(sesion);

        if (sesion.Perfil == Perfiles.Operador)
            throw new UnauthorizedAccessException("Solo el comercio o el administrador del operador configuran avisos.");

        var cuentaId = cuentaSesion ?? command.CuentaComercialId ?? throw new ArgumentException("Hay que indicar la cuenta comercial.");

        _ = await cuentas.ObtenerAsync(cuentaId, ct)
            ?? throw new NoEncontradoException("La cuenta comercial no existe."); // el filtro por operador impide usar una cuenta de otro

        // El secreto lo genera la plataforma: con él el comercio verifica la firma de cada aviso.
        var secreto = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var s = SuscripcionAviso.Crear(operadorId, cuentaId, command.Url, secreto, command.TiposEvento);
        await suscripciones.AgregarAsync(s, ct);
        await unitOfWork.GuardarCambiosAsync(ct);

        return s.Id;
    }
}
