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
}