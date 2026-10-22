
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
}
