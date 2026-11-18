using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.CuentasComerciales.Commands.ModificarComercio;

public sealed class ModificarComercioHandler(
    ICuentaComercialRepository cuentas,
    IComercioRepository comercios,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    IValidator<ModificarComercioCommand> validator)
{
    public async Task Handle(ModificarComercioCommand command, CancellationToken ct)
    {
        // Mismo actor que acepta y rechaza las solicitudes.
        if (!tenant.EstaAutenticado ||
            tenant.OperadorId is not Guid operadorId ||
            !Perfiles.Configuracion.Contains(tenant.Perfil))
            throw new UnauthorizedAccessException(
                "Solo el administrador del operador puede modificar comercios.");

        await validator.ValidateAndThrowAsync(command, ct);

        // El operador solo llega a los comercios con los que tiene una cuenta aceptada.
        var cuenta = await cuentas.ObtenerAsync(command.CuentaComercialId, ct);
        if (cuenta is null || cuenta.OperadorId != operadorId)
            throw new NoEncontradoException("El comercio no existe.");

        if (cuenta.Estado != EstadoCuentaComercial.Confirmada)
            throw new InvalidOperationException("Solo se pueden modificar comercios aceptados.");

        var comercio = await comercios.ObtenerAsync(cuenta.ComercioId, ct)
            ?? throw new NoEncontradoException("El comercio no existe.");

        comercio.Modificar(command.Nombre);

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
