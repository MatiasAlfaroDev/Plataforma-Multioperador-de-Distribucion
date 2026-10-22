
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface IVehiculoRepository
{
    Task<Vehiculo?> ObtenerAsync(
        Guid operadorId,
        Guid vehiculoId,
        CancellationToken ct);

    Task<IReadOnlyList<Vehiculo>> ListarActivosAsync(
        Guid operadorId,
        CancellationToken ct);
}
