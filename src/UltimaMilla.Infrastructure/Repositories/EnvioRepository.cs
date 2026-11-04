using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class EnvioRepository(AppDbContext db) : IEnvioRepository
{
    public async Task AgregarAsync(Envio envio, CancellationToken ct) =>
        await db.Envios.AddAsync(envio, ct);

    public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Envios
            .Include(e => e.Bultos)
            .Include(e => e.Intentos)
            .Include(e => e.Eventos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct) =>
        db.Envios.AnyAsync(e => e.CuentaComercialId == cuentaComercialId && e.ReferenciaExterna == referenciaExterna, ct);

    public async Task<IReadOnlyList<Envio>> ListarAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct)
    {
        var consulta = db.Envios
            .AsNoTracking()
            .Include(e => e.Bultos)
            .Where(e => e.OperadorId == operadorId);

        if (cuentaComercialId.HasValue)
        {
            consulta = consulta.Where(
                e => e.CuentaComercialId == cuentaComercialId.Value);
        }

        return await consulta
            .OrderByDescending(e => e.FechaAlta)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Envio>> ListarParaPlanificacionAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await db.Envios
            .AsNoTracking()
            .AsSplitQuery()
            .Include(e => e.Bultos)
            .Include(e => e.Intentos)
            .Where(e => e.OperadorId == operadorId &&
                (e.Estado == EstadoEnvio.EnDeposito ||
                e.Estado == EstadoEnvio.Reprogramado))
            .OrderByDescending(e => e.FechaAlta)
            .ToListAsync(ct);
    }
}
