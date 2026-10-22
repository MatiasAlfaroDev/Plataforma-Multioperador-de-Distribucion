
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface IRepartidorRepository
{
    Task<Repartidor?> ObtenerAsync(
        Guid operadorId,
        Guid repartidorId,
        CancellationToken ct);

    Task<IReadOnlyList<Repartidor>> ListarActivosAsync(
        Guid operadorId,
        CancellationToken ct);
}
