using UltimaMilla.Domain.Reportes;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Cierres diarios del operador actual (filtro global por operador).</summary>
public interface ICierreDiarioRepository
{
    /// <summary>El cierre de esa fecha, con seguimiento de cambios para recalcularlo.</summary>
    Task<CierreDiario?> ObtenerAsync(DateOnly fecha, CancellationToken ct);

    Task<IReadOnlyList<CierreDiario>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);

    Task AgregarAsync(CierreDiario cierre, CancellationToken ct);
}
