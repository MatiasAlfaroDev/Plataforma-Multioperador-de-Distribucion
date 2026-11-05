using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Commands.AceptarVinculacion;

public sealed class AceptarVinculacionHandler(
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant)
{
    public async Task Handle(
        AceptarVinculacionCommand command,
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
                "Solo el administrador del operador puede aceptar solicitudes.");
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

        // 4. Confirmar mediante el dominio.
        solicitud.Confirmar();

        // 5. Persistir.
        await unitOfWork.GuardarCambiosAsync(ct);
    }
}