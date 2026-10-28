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

    /// <summary>Las hojas de ruta de hoy del repartidor logueado, con los bultos de cada parada (para cargar el vehículo).</summary>
    Task<IReadOnlyList<HojaDeRutaItem>> ListarMisHojasAsync(CancellationToken ct = default);

    /// <summary>Despacha la hoja con los bultos escaneados. Devuelve si salió bien y el mensaje para mostrar.</summary>
    Task<(bool Ok, string Mensaje)> DespacharAsync(Guid hojaId, IReadOnlyList<string> bultosEscaneados, CancellationToken ct = default);

    /// <summary>Manda al servidor un evento registrado en el teléfono (entrega o intento fallido).</summary>
    Task<(ResultadoEnvio Resultado, string? Detalle)> EnviarEventoAsync(EventoPendiente evento, CancellationToken ct = default);
}
