
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

    // Con seguimiento (sin AsNoTracking): EF Core detecta las coberturas y franjas
    // que se quitan y se agregan, y las guarda en GuardarCambiosAsync.
    public async Task<ZonaCobertura?> ObtenerAsync(
        Guid operadorId,
        Guid zonaId,
        CancellationToken ct)
    {
        return await db.ZonasCobertura
            .Include(z => z.Localidades)
            .Include(z => z.Franjas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(z => z.OperadorId == operadorId && z.Id == zonaId, ct);
    }

    public async Task<bool> ExisteNombreEnOtraZonaAsync(
        Guid operadorId,
        string nombre,
        Guid zonaId,
        CancellationToken ct)
    {
        var normalizado = nombre.Trim().ToLowerInvariant();

        return await db.ZonasCobertura
            .AnyAsync(z =>
                z.OperadorId == operadorId &&
                z.Id != zonaId &&
                z.Nombre.ToLower() == normalizado,
                ct);
    }
}
