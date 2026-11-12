using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class RetiroRepository(AppDbContext db) : IRetiroRepository
{
    public async Task AgregarAsync(
        Retiro retiro,
        CancellationToken ct)
    {
        await db.Retiros.AddAsync(retiro, ct);
    }

    public Task<Retiro?> ObtenerAsync(
        Guid id,
        CancellationToken ct)
    {
        return db.Retiros
            .Include(r => r.Bultos)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<Retiro>> ListarAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct)
    {
        var consulta = db.Retiros
            .AsNoTracking()
            .Include(r => r.Bultos)
            .Where(r => r.OperadorId == operadorId);

        if (cuentaComercialId.HasValue)
        {
            consulta = consulta.Where(
                r => r.CuentaComercialId == cuentaComercialId.Value);
        }

        return await consulta
            .OrderByDescending(r => r.SolicitadoEn)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Retiro>> ListarParaPlanificacionAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await db.Retiros
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Bultos)
            .Where(r =>
                r.OperadorId == operadorId &&
                r.Estado == EstadoRetiro.Solicitado &&
                r.HojaDeRutaId == null)
            .OrderBy(r => r.FechaSolicitada)
            .ThenBy(r => r.HoraDesde)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Retiro>> ListarParaEntregaComercioAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await db.Retiros
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Bultos)
            .Where(r =>
                r.OperadorId == operadorId &&
                r.Estado == EstadoRetiro.EnDeposito &&
                !db.Set<ParadaRuta>().Any(p =>
                    p.RetiroId == r.Id &&
                    p.LiberadaEn == null))
            .OrderBy(r => r.FechaSolicitada)
            .ThenBy(r => r.HoraDesde)
            .ToListAsync(ct);
    }
}