using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos;

/// <summary>
/// DTO para representar una entrega de aviso en la capa de aplicación.
/// </summary>
public sealed record EntregaAvisoDto(
    Guid Id,
    Guid CuentaComercialId,
    string TipoEvento,
    string ClaveEvento,
    string Estado,
    int Intentos,
    string? UltimoError,
    DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoIntentoEn,
    DateTimeOffset? EntregadaEn)
{
    /// <summary>
    /// Mapea una entidad de entrega a su DTO correspondiente.
    /// </summary>
    public static EntregaAvisoDto Desde(EntregaAviso e) => new(
        e.Id,
        e.CuentaComercialId,
        e.TipoEvento,
        e.ClaveEvento,
        e.Estado.ToString(),
        e.Intentos,
        e.UltimoError,
        e.CreadoEn,
        e.UltimoIntentoEn,
        e.EntregadaEn);
}
