using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;

public sealed class ListarCuentasComercialesHandler(ICuentaComercialRepository cuentas)
{
    public Task<IReadOnlyList<CuentaComercialDto>> Handle(ListarCuentasComercialesQuery query, CancellationToken ct) =>
        cuentas.ListarResumenAsync(ct);
}
