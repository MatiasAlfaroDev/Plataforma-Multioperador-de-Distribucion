using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class MotivoNoEntregaRepository(AppDbContext db)
    : IMotivoNoEntregaRepository
{
    public Task<MotivoNoEntrega?> ObtenerPorCodigoAsync(
        Guid operadorId,
        Guid versionReglasOperativasId,
        string codigo,
        CancellationToken ct)
    {
        return db.Set<MotivoNoEntrega>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.OperadorId == operadorId &&
                m.VersionReglasOperativasId == versionReglasOperativasId &&
                m.Codigo == codigo,
                ct);
    }

    public Task<MotivoNoEntrega?> ObtenerPorDescripcionAsync(
        Guid operadorId,
        Guid versionReglasOperativasId,
        string descripcion,
        CancellationToken ct)
    {
        var descripcionNormalizada = descripcion.Trim();

        return db.Set<MotivoNoEntrega>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.OperadorId == operadorId &&
                m.VersionReglasOperativasId == versionReglasOperativasId &&
                m.Descripcion == descripcionNormalizada,
                ct);
    }

    public async Task<IReadOnlyList<MotivoNoEntrega>> ListarPorVersionAsync(
        Guid operadorId,
        Guid versionReglasOperativasId,
        CancellationToken ct)
    {
        return await db.Set<MotivoNoEntrega>()
            .AsNoTracking()
            .Where(m =>
                m.OperadorId == operadorId &&
                m.VersionReglasOperativasId == versionReglasOperativasId)
            .OrderBy(m => m.Codigo)
            .ToListAsync(ct);
    }

    public async Task AgregarRangoAsync(
        IEnumerable<MotivoNoEntrega> motivos,
        CancellationToken ct)
    {
        await db.Set<MotivoNoEntrega>()
            .AddRangeAsync(motivos, ct);
    }

}