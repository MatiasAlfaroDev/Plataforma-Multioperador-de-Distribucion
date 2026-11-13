using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class DiscrepanciaRecepcionRepository(AppDbContext db) : IDiscrepanciaRecepcionRepository
{
    public async Task AgregarAsync(DiscrepanciaRecepcion discrepancia, CancellationToken ct) =>
        await db.DiscrepanciasRecepcion.AddAsync(discrepancia, ct);

    public async Task<IReadOnlyList<DiscrepanciaRecepcion>> ListarPorEnvioAsync(
        Guid envioId,
        CancellationToken ct) =>
        await db.DiscrepanciasRecepcion.AsNoTracking()
            .Where(d => d.EnvioId == envioId)
            .OrderBy(d => d.RegistradaEn)
            .ThenBy(d => d.Identificacion)
            .ToListAsync(ct);
}
