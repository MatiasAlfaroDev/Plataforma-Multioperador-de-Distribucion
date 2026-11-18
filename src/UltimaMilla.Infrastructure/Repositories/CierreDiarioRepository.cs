using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Reportes;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class CierreDiarioRepository(AppDbContext db) : ICierreDiarioRepository
{
    public Task<CierreDiario?> ObtenerAsync(DateOnly fecha, CancellationToken ct) =>
        db.CierresDiarios.FirstOrDefaultAsync(c => c.Fecha == fecha, ct);

    public async Task<IReadOnlyList<CierreDiario>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        await db.CierresDiarios.AsNoTracking()
            .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
            .OrderBy(c => c.Fecha)
            .ToListAsync(ct);

    public async Task AgregarAsync(CierreDiario cierre, CancellationToken ct) =>
        await db.CierresDiarios.AddAsync(cierre, ct);
}
