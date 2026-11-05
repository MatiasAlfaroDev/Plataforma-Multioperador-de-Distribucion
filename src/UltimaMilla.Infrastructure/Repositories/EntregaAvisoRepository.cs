using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class EntregaAvisoRepository(AppDbContext db) : IEntregaAvisoRepository
{
    public Task<EntregaAviso?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.EntregasAviso.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExisteAsync(Guid suscripcionId, string claveEvento, CancellationToken ct) =>
        db.EntregasAviso.AnyAsync(e => e.SuscripcionId == suscripcionId && e.ClaveEvento == claveEvento, ct);

    public async Task<IReadOnlyList<EntregaAviso>> ListarAsync(Guid? cuentaComercialId, EstadoEntregaAviso? estado, CancellationToken ct)
    {
        var query = db.EntregasAviso.AsQueryable();

        if (cuentaComercialId != null)
            query = query.Where(e => e.CuentaComercialId == cuentaComercialId);

        if (estado != null)
            query = query.Where(e => e.Estado == estado);

        return await query
            .OrderByDescending(e => e.CreadoEn)
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EntregaAviso>> ListarFallidasAsync(CancellationToken ct) =>
        await db.EntregasAviso
            .Where(e => e.Estado == EstadoEntregaAviso.Fallida && e.Intentos < EntregaAviso.MaximoIntentos)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> OperadoresConFallidasAsync(CancellationToken ct) =>
        await db.EntregasAviso
            .IgnoreQueryFilters()
            .Where(e => e.Estado == EstadoEntregaAviso.Fallida && e.Intentos < EntregaAviso.MaximoIntentos)
            .Select(e => e.OperadorId)
            .Distinct()
            .ToListAsync(ct);

    public async Task AgregarAsync(EntregaAviso entrega, CancellationToken ct) =>
        await db.EntregasAviso.AddAsync(entrega, ct);
}
