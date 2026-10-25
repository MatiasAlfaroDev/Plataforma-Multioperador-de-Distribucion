using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface ICuentaComercialRepository
{
    Task<CuentaComercial?> ObtenerAsync(
        Guid id,
        CancellationToken ct);

    Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct);
}