using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Gestion;

public sealed class ListarComerciosDelOperadorHandler(
    IComerciosDelOperadorLectura lectura,
    ITenantContext tenant)
{
    public Task<IReadOnlyList<ComercioDelOperadorDto>> Handle(
        ListarComerciosDelOperadorQuery query,
        CancellationToken ct)
    {
        if (!tenant.EstaAutenticado || !tenant.OperadorId.HasValue)
            throw new UnauthorizedAccessException("Debe iniciar sesión como operador.");

        if (tenant.Perfil != Perfiles.AdministradorOperador)
            throw new UnauthorizedAccessException(
                "Solo un administrador del operador puede gestionar comercios.");

        return lectura.ListarAsync(tenant.OperadorId.Value, ct);
    }
}
