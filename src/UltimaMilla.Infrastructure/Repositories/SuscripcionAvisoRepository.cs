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

    public async Task<IReadOnlyList<SuscripcionAviso>> ListarAsync(IReadOnlyCollection<Guid>? cuentasComerciales, CancellationToken ct)
    {
        // La sesión del comercio no tiene operador: sin ignorar el filtro no vería nada. Las cuentas
        // ya vienen validadas como suyas (AlcanceAvisos).
        var query = cuentasComerciales is null
            ? db.SuscripcionesAviso
            : db.SuscripcionesAviso.IgnoreQueryFilters().Where(s => cuentasComerciales.Contains(s.CuentaComercialId));

        return await query
            .OrderBy(s => s.CreadoEn)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(SuscripcionAviso suscripcion, CancellationToken ct) =>
        await db.SuscripcionesAviso.AddAsync(suscripcion, ct);
}
