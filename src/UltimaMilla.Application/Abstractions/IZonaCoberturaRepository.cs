using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Abstractions;

public interface IZonaCoberturaRepository
{
    Task<IReadOnlyList<ZonaCobertura>> ListarPorOperadorAsync(
        Guid operadorId,
        CancellationToken ct);
}