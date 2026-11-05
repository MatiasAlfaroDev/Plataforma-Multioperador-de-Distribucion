using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Commands.RechazarVinculacion;

public sealed class RechazarVinculacionHandler(
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant)
{
    public async Task Handle(
        RechazarVinculacionCommand command,
        CancellationToken ct)
    {
        // 1. Verificar autenticación y operador.
        if (!tenant.EstaAutenticado || !tenant.OperadorId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión como operador.");
        }

        // 2. Verificar permisos.
        if (tenant.Perfil != Perfiles.AdministradorOperador)
        {
            throw new UnauthorizedAccessException(
                "Solo el administrador del operador puede rechazar solicitudes.");
        }

        // 3. Buscar la solicitud.
        var solicitud = await cuentas.ObtenerAsync(
            command.SolicitudId,
            ct);

        if (solicitud is null ||
            solicitud.OperadorId != tenant.OperadorId.Value)
        {
            throw new KeyNotFoundException(
                "La solicitud no existe.");
        }

        // 4. Rechazar mediante el dominio.
        solicitud.Rechazar();

        // 5. Persistir.
        await unitOfWork.GuardarCambiosAsync(ct);
    }
}