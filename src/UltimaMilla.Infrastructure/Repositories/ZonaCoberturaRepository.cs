
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
            .Include(z => z.Localidades)
            .Include(z => z.Franjas)
            .AsSplitQuery()
            .Where(z => z.OperadorId == operadorId && z.Activa)
            .OrderBy(z => z.Nombre)
            .ToListAsync(ct);
    }

    public async Task<bool> ExisteNombreAsync(
        Guid operadorId,
        string nombre,
        CancellationToken ct)
    {
        var normalizado = nombre.Trim().ToLowerInvariant();

        return await db.ZonasCobertura
            .AnyAsync(z =>
                z.OperadorId == operadorId &&
                z.Nombre.ToLower() == normalizado,
                ct);
    }

    public async Task AgregarAsync(
        ZonaCobertura zona,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(zona);

        await db.ZonasCobertura.AddAsync(zona, ct);
    }
}
