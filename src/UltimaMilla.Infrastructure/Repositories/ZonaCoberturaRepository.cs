using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class ZonaCoberturaRepository(AppDbContext db)
    : IZonaCoberturaRepository
{
    public async Task<IReadOnlyList<ZonaCobertura>> ListarPorOperadorAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await db.ZonasCobertura
            .AsNoTracking()
            .Where(z => z.OperadorId == operadorId && z.Activa)
            .OrderBy(z => z.Nombre)
            .ToListAsync(ct);
    }
}