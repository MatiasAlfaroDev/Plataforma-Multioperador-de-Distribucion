
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class VehiculoRepository(AppDbContext db)
    : IVehiculoRepository
{
    public Task<Vehiculo?> ObtenerAsync(
        Guid operadorId,
        Guid vehiculoId,
        CancellationToken ct)
    {
        return db.Vehiculos.FirstOrDefaultAsync(
            x => x.Id == vehiculoId &&
                 x.OperadorId == operadorId &&
                 x.Activo,
            ct);
    }

    public async Task<IReadOnlyList<Vehiculo>> ListarActivosAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await db.Vehiculos
            .AsNoTracking()
            .Where(x => x.OperadorId == operadorId && x.Activo)
            .OrderBy(x => x.Matricula)
            .ToListAsync(ct);
    }
}
