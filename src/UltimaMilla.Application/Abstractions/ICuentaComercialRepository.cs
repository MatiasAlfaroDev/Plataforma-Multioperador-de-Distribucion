using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface ICuentaComercialRepository
{
    Task<CuentaComercial?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>Cuentas con el nombre del operador y del comercio, para mostrar en pantalla.</summary>
    Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(CancellationToken ct);
}
