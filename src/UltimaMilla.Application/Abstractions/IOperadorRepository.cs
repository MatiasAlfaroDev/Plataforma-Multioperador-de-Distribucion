
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface IOperadorRepository
{
    Task<IReadOnlyList<Operador>> ListarAsync(
        CancellationToken ct);

    Task<Operador?> ObtenerAsync(
        Guid id,
        CancellationToken ct);

    // Recupera el operador con seguimiento de EF Core.
    Task<Operador?> ObtenerParaActualizarAsync(
        Guid id,
        CancellationToken ct);
}
