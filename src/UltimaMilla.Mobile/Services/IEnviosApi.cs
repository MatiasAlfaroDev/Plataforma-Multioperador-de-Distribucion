using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>Cómo terminó el intento de mandar un evento al servidor.</summary>
public enum ResultadoEnvio
{
    /// <summary>El servidor lo guardó (o ya lo tenía: el reintento es seguro).</summary>
    Aceptado,
    /// <summary>El servidor respondió pero no lo aceptó (por ejemplo, datos inválidos). Queda en la cola con el motivo.</summary>
    Rechazado,
    /// <summary>No hubo respuesta (sin señal, servidor caído). Se reintenta más tarde.</summary>
    SinConexion
}

/// <summary>
/// Contrato con la API (como IOrdersApi en la demo BeatScan). Los ViewModels usan
/// la interfaz, no la implementación: así se pueden probar sin red.
/// </summary>
public interface IEnviosApi
{
    Task<IReadOnlyList<OperadorItem>> ListarOperadoresAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EnvioItem>> ListarEnviosAsync(Guid operadorId, CancellationToken ct = default);

    /// <summary>Manda al servidor un evento registrado en el teléfono (entrega o intento fallido).</summary>
    Task<(ResultadoEnvio Resultado, string? Detalle)> EnviarEventoAsync(EventoPendiente evento, CancellationToken ct = default);
}
