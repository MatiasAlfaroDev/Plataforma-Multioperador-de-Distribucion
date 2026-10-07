using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Contrato con la API (como IOrdersApi en la demo BeatScan). Los ViewModels usan
/// la interfaz, no la implementación: así se pueden probar sin red.
/// </summary>
public interface IEnviosApi
{
    Task<IReadOnlyList<OperadorItem>> ListarOperadoresAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EnvioItem>> ListarEnviosAsync(Guid operadorId, CancellationToken ct = default);
}
