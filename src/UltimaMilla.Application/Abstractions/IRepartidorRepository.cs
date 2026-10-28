
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface IRepartidorRepository
{
    Task<Repartidor?> ObtenerAsync(
        Guid operadorId,
        Guid repartidorId,
        CancellationToken ct);

    /// <summary>El repartidor activo vinculado a ese usuario (el que inició sesión en la app).</summary>
    Task<Repartidor?> ObtenerPorUsuarioAsync(
        Guid operadorId,
        Guid usuarioId,
        CancellationToken ct);

    Task<IReadOnlyList<Repartidor>> ListarActivosAsync(
        Guid operadorId,
        CancellationToken ct);
}
