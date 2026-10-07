using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class VersionTarifariaRepository(AppDbContext db)
    : IVersionTarifariaRepository
{
    public async Task<VersionTarifaria?> ObtenerVigenteAsync(
        Guid operadorId,
        string departamento,
        DateTimeOffset fecha,
        CancellationToken ct)
    {
        return await db.VersionesTarifarias
            .AsNoTracking()
            .Where(v =>
                v.OperadorId == operadorId &&
                v.VigenciaDesde <= fecha &&
                (v.VigenciaHasta == null || fecha < v.VigenciaHasta))
            .Join(
                db.ZonasCobertura,
                version => version.ZonaCoberturaId,
                zona => zona.Id,
                (version, zona) => new { version, zona })
            .Where(x =>
                x.zona.OperadorId == operadorId &&
                x.zona.Activa &&
                x.zona.Departamento == departamento)
            .Select(x => x.version)
            .OrderByDescending(v => v.VigenciaDesde)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<VersionTarifaria?> ObtenerVigentePorZonaAsync(
        Guid operadorId,
        Guid zonaCoberturaId,
        DateTimeOffset fecha,
        CancellationToken ct)
    {
        return await db.VersionesTarifarias
            .Where(v =>
                v.OperadorId == operadorId &&
                v.ZonaCoberturaId == zonaCoberturaId &&
                v.VigenciaDesde <= fecha &&
                (v.VigenciaHasta == null || fecha < v.VigenciaHasta))
            .OrderByDescending(v => v.VigenciaDesde)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AgregarAsync(
        VersionTarifaria version,
        CancellationToken ct)
    {
        await db.VersionesTarifarias.AddAsync(version, ct);
    }
}