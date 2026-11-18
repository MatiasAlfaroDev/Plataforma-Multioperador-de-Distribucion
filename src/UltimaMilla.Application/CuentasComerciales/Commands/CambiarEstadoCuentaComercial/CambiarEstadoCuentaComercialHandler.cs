using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Commands.CambiarEstadoCuentaComercial;

/// <summary>
/// La baja es de la cuenta (operador + comercio) y no del comercio: el comercio es global y puede
/// seguir trabajando con otros operadores (ADR-002). Los envíos y retiros ya creados no se tocan.
/// </summary>
public sealed class CambiarEstadoCuentaComercialHandler(
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant)
{
    public async Task Handle(CambiarEstadoCuentaComercialCommand command, CancellationToken ct)
    {
        if (!tenant.EstaAutenticado ||
            tenant.OperadorId is not Guid operadorId ||
            !Perfiles.Configuracion.Contains(tenant.Perfil))
            throw new UnauthorizedAccessException(
                "Solo el administrador del operador puede dar de baja comercios.");

        var cuenta = await cuentas.ObtenerAsync(command.CuentaComercialId, ct);
        if (cuenta is null || cuenta.OperadorId != operadorId)
            throw new NoEncontradoException("El comercio no existe.");

        if (command.Activa)
            cuenta.Reactivar();
        else
            cuenta.Desactivar();

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
