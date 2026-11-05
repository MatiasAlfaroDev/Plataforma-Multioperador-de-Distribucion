using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales
    .Queries.ListarSolicitudesPendientes;

public sealed class ListarSolicitudesPendientesHandler(
    ICuentaComercialRepository cuentas,
    ITenantContext tenant)
{
    public Task<IReadOnlyList<SolicitudPendienteDto>> Handle(
        ListarSolicitudesPendientesQuery query,
        CancellationToken ct)
    {
        if (!tenant.EstaAutenticado ||
            !tenant.OperadorId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe iniciar sesión como operador.");
        }

        if (tenant.Perfil != Perfiles.AdministradorOperador)
        {
            throw new UnauthorizedAccessException(
                "Solo un administrador del operador puede consultar las solicitudes.");
        }

        return cuentas.ListarSolicitudesPendientesAsync(
            tenant.OperadorId.Value,
            ct);
    }
}
