using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class ReporteRetirosRepository(AppDbContext db)
    : IReporteRetirosRepository
{
    public async Task<IReadOnlyList<ConteoRetirosPorEstado>>
        ContarPorEstadoAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct)
    {
        var datos = await db.Retiros
            .AsNoTracking()
            .Where(r =>
                r.OperadorId == operadorId &&
                r.SolicitadoEn >= desde &&
                r.SolicitadoEn < hastaExclusivo)
            .GroupBy(r => r.Estado)
            .Select(g => new
            {
                Estado = g.Key,
                Cantidad = g.Count()
            })
            .ToListAsync(ct);

        return datos
            .Select(x => new ConteoRetirosPorEstado(
                x.Estado.ToString(),
                x.Cantidad))
            .OrderBy(x => x.Estado)
            .ToList();
    }

    public Task<decimal> ObtenerTotalTarifadoAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct)
    {
        return db.Retiros
            .AsNoTracking()
            .Where(r =>
                r.OperadorId == operadorId &&
                r.SolicitadoEn >= desde &&
                r.SolicitadoEn < hastaExclusivo)
            .SumAsync(
                r => r.TarifaCalculada ?? 0m,
                ct);
    }
}