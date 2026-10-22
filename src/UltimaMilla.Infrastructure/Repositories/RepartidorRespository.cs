
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class RepartidorRepository(AppDbContext db)
    : IRepartidorRepository
{
    public Task<Repartidor?> ObtenerAsync(
        Guid operadorId,
        Guid repartidorId,
        CancellationToken ct)
    {
        return db.Repartidores.FirstOrDefaultAsync(
            x => x.Id == repartidorId &&
                 x.OperadorId == operadorId &&
                 x.Activo,
            ct);
    }

    public async Task<IReadOnlyList<Repartidor>> ListarActivosAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await db.Repartidores
            .AsNoTracking()
            .Where(x => x.OperadorId == operadorId && x.Activo)
            .OrderBy(x => x.Nombre)
            .ToListAsync(ct);
    }
}
