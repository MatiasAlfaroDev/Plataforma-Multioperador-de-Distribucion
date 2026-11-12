using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;

public sealed class ListarCuentasComercialesHandler(
    ICuentaComercialRepository cuentas,
    ITenantContext tenant)
{
    public Task<IReadOnlyList<CuentaComercialDto>> Handle(
    ListarCuentasComercialesQuery query,
    CancellationToken ct)
{
    if (!tenant.EstaAutenticado)
        throw new UnauthorizedAccessException(
            "Debe iniciar sesión.");

    if (tenant.Perfil is Perfiles.AdministradorComercio or "Comercio")
    {
        if (!tenant.ComercioId.HasValue)
            throw new UnauthorizedAccessException(
                "El usuario no tiene un comercio asociado.");

        return cuentas.ListarPorComercioAsync(
            tenant.ComercioId.Value,
            ct);
    }

    if (tenant.OperadorId.HasValue &&
        Perfiles.PersonalOperador.Contains(tenant.Perfil))
    {
        return cuentas.ListarResumenAsync(
            tenant.OperadorId.Value,
            null,
            ct);
    }

    throw new UnauthorizedAccessException(
        "No tiene permisos para consultar cuentas comerciales.");
}
}