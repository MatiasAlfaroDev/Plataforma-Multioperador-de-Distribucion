using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos;

/// <summary>
/// DTO para representar una suscripción a avisos en la capa de aplicación.
/// </summary>
public sealed record SuscripcionAvisoDto(
    Guid Id,
    Guid CuentaComercialId,
    string Url,
    string Secreto,
    IReadOnlyList<string> TiposEvento,
    bool Activa)
{
    /// <summary>
    /// Mapea una entidad de suscripción a su DTO correspondiente.
    /// </summary>
    public static SuscripcionAvisoDto Desde(SuscripcionAviso s) => new(
        s.Id,
        s.CuentaComercialId,
        s.Url,
        s.Secreto,
        s.TiposEvento,
        s.Activa);
}
