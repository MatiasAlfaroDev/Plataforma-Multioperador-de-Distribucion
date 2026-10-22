using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class VersionReglasOperativasRepository(AppDbContext db)
    : IVersionReglasOperativasRepository
{
    public async Task<VersionReglasOperativas?> ObtenerVigenteAsync(
        Guid operadorId,
        DateTimeOffset fecha,
        CancellationToken ct)
    {
        return await db.VersionesReglasOperativas
            .AsNoTracking()
            .Where(v =>
                v.OperadorId == operadorId &&
                v.VigenciaDesde <= fecha &&
                (v.VigenciaHasta == null || fecha < v.VigenciaHasta))
            .OrderByDescending(v => v.VigenciaDesde)
            .FirstOrDefaultAsync(ct);
    }

    public Task<VersionReglasOperativas?> ObtenerPorIdAsync(
        Guid operadorId,
        Guid versionId,
        CancellationToken ct)
    {
        return db.VersionesReglasOperativas
            .AsNoTracking()
            .FirstOrDefaultAsync(v =>
                v.Id == versionId &&
                v.OperadorId == operadorId,
                ct);
    }
}
