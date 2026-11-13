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

    public Task<EntregaAviso?> ObtenerAsync(Guid id, IReadOnlyCollection<Guid>? cuentasComerciales, CancellationToken ct) =>
        cuentasComerciales is null
            ? ObtenerAsync(id, ct)
            : db.EntregasAviso.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == id && cuentasComerciales.Contains(e.CuentaComercialId), ct);

    public async Task<IReadOnlyList<EntregaAviso>> ListarAsync(IReadOnlyCollection<Guid>? cuentasComerciales, EstadoEntregaAviso? estado, CancellationToken ct)
    {
        // Sesión de comercio: sin operador, se ignora el filtro y se restringe a sus cuentas (AlcanceAvisos).
        var query = cuentasComerciales is null
            ? db.EntregasAviso.AsQueryable()
            : db.EntregasAviso.IgnoreQueryFilters().Where(e => cuentasComerciales.Contains(e.CuentaComercialId));

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
