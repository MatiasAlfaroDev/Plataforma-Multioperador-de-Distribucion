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
        if (!tenant.EstaAutenticado ||
            !tenant.OperadorId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Debe seleccionar un operador.");
        }

        var operadorId = tenant.OperadorId.Value;

        Guid? cuentaComercialId = null;

        if (tenant.Perfil == "Comercio")
        {
            if (!tenant.ComercioId.HasValue)
            {
                throw new UnauthorizedAccessException(
                    "El usuario no tiene una cuenta comercial activa.");
            }

            cuentaComercialId = tenant.ComercioId.Value;
        }

        return cuentas.ListarResumenAsync(
            operadorId,
            cuentaComercialId,
            ct);
    }
}