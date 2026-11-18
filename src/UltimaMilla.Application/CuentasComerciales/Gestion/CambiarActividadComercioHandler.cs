using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Gestion;

/// <summary>
/// Baja y reactivación de un comercio del operador. La baja impide crear envíos,
/// retiros y hojas de ruta nuevos; lo ya en curso no se toca.
/// </summary>
public sealed class CambiarActividadComercioHandler(
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant)
{
    public async Task Handle(
        CambiarActividadComercioCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!tenant.EstaAutenticado || !tenant.OperadorId.HasValue)
            throw new UnauthorizedAccessException("Debe iniciar sesión como operador.");

        if (tenant.Perfil != Perfiles.AdministradorOperador)
            throw new UnauthorizedAccessException(
                "Solo el administrador del operador puede dar de baja o reactivar comercios.");

        var cuenta = await cuentas.ObtenerAsync(command.CuentaId, ct);

        if (cuenta is null || cuenta.OperadorId != tenant.OperadorId.Value)
            throw new KeyNotFoundException("El comercio no está vinculado a este operador.");

        if (command.Activa)
            cuenta.Reactivar();
        else
            cuenta.Desactivar();

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
