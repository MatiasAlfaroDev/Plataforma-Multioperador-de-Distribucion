using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

/// <summary>
/// El filtro por operador (AppDbContext) se aplica solo: cada operador ve únicamente sus suscripciones.
/// </summary>
public sealed class SuscripcionAvisoRepository(AppDbContext db) : ISuscripcionAvisoRepository
{
    public Task<SuscripcionAviso?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.SuscripcionesAviso.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SuscripcionAviso>> ListarAsync(Guid? cuentaComercialId, CancellationToken ct)
    {
        return await db.SuscripcionesAviso
            .Where(s => cuentaComercialId == null || s.CuentaComercialId == cuentaComercialId)
            .OrderBy(s => s.CreadoEn)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(SuscripcionAviso suscripcion, CancellationToken ct) =>
        await db.SuscripcionesAviso.AddAsync(suscripcion, ct);
}
