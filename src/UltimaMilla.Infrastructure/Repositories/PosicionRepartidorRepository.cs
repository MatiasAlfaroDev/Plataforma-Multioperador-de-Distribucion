using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class PosicionRepartidorRepository(AppDbContext db) : IPosicionRepartidorRepository
{
    public async Task<IReadOnlySet<Guid>> ExistentesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var existentes = await db.PosicionesRepartidor
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        return existentes.ToHashSet();
    }

    public async Task AgregarAsync(IEnumerable<PosicionRepartidor> posiciones, CancellationToken ct) =>
        await db.PosicionesRepartidor.AddRangeAsync(posiciones, ct);

    public async Task<IReadOnlyList<PosicionRepartidor>> ListarRecorridoAsync(
        Guid operadorId, Guid repartidorId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct) =>
        await db.PosicionesRepartidor
            .AsNoTracking()
            .Where(p => p.OperadorId == operadorId &&
                        p.RepartidorId == repartidorId &&
                        p.RegistradaEn >= desde &&
                        p.RegistradaEn < hasta)
            .OrderBy(p => p.RegistradaEn)
            .ToListAsync(ct);
}
