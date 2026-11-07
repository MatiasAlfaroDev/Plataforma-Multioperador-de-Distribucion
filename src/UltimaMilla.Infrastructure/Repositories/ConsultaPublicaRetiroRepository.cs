using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class ConsultaPublicaRetiroRepository(AppDbContext db)
    : IConsultaPublicaRetiroRepository
{
    public Task<SeguimientoPublicoRetiroDto?> ObtenerAsync(
        Guid operadorId,
        Guid retiroId,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty || retiroId == Guid.Empty)
            return Task.FromResult<SeguimientoPublicoRetiroDto?>(null);

        return db.Retiros
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                r.Id == retiroId &&
                r.OperadorId == operadorId)
            .Select(r => new SeguimientoPublicoRetiroDto(
                r.Estado.ToString(),
                r.SolicitadoEn,
                r.FechaSolicitada,
                r.RetiradoEn))
            .FirstOrDefaultAsync(ct);
    }
}