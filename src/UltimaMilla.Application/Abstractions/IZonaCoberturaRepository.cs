
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Abstractions;

public interface IZonaCoberturaRepository
{
    Task<IReadOnlyList<ZonaCobertura>> ListarPorOperadorAsync(
        Guid operadorId,
        CancellationToken ct);

    Task<bool> ExisteNombreAsync(
        Guid operadorId,
        string nombre,
        CancellationToken ct);

    Task AgregarAsync(
        ZonaCobertura zona,
        CancellationToken ct);

    /// <summary>La zona con sus coberturas y franjas, lista para modificar y guardar.</summary>
    Task<ZonaCobertura?> ObtenerAsync(
        Guid operadorId,
        Guid zonaId,
        CancellationToken ct);

    /// <summary>True si otra zona del operador (distinta de zonaId) ya usa ese nombre.</summary>
    Task<bool> ExisteNombreEnOtraZonaAsync(
        Guid operadorId,
        string nombre,
        Guid zonaId,
        CancellationToken ct);
}
