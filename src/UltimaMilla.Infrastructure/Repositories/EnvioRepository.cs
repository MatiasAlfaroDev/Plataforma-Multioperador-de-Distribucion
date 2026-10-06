using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class EnvioRepository(AppDbContext db) : IEnvioRepository
{
    public async Task AgregarAsync(Envio envio, CancellationToken ct) =>
        await db.Envios.AddAsync(envio, ct);

    public Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct) =>
        db.Envios.AnyAsync(e => e.CuentaComercialId == cuentaComercialId && e.ReferenciaExterna == referenciaExterna, ct);

    public async Task<IReadOnlyList<Envio>> ListarAsync(Guid? operadorId, CancellationToken ct)
    {
        var consulta = db.Envios.AsNoTracking().Include(e => e.Bultos).AsQueryable();

        if (operadorId is Guid id)
            consulta = consulta.Where(e => e.OperadorId == id);

        return await consulta.OrderByDescending(e => e.FechaAlta).ToListAsync(ct);
    }
}
