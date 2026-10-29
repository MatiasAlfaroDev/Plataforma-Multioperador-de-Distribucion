
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class HojaDeRutaRepository(AppDbContext db)
    : IHojaDeRutaRepository
{
    public async Task AgregarAsync(
        HojaDeRuta hoja,
        CancellationToken ct)
    {
        await db.HojasDeRuta.AddAsync(hoja, ct);
    }

    public Task<HojaDeRuta?> ObtenerAsync(
        Guid operadorId,
        Guid hojaId,
        CancellationToken ct)
    {
        return db.HojasDeRuta
            .Include(x => x.Paradas)
            .FirstOrDefaultAsync(
                x => x.Id == hojaId &&
                     x.OperadorId == operadorId,
                ct);
    }

    public async Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await db.HojasDeRuta
            .AsNoTracking()
            .Include(x => x.Paradas)
            .Where(x => x.OperadorId == operadorId)
            .OrderByDescending(x => x.Fecha)
            .ToListAsync(ct);
    }

    public Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
        Guid operadorId,
        Guid envioId,
        CancellationToken ct)
    {
        return db.HojasDeRuta
            .Include(x => x.Paradas)
            .SingleOrDefaultAsync(
                x => x.OperadorId == operadorId &&
                    x.Paradas.Any(p =>
                        p.EnvioId == envioId &&
                        p.LiberadaEn == null),
                ct);
    }
}
