using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class EnvioRepository(AppDbContext db) : IEnvioRepository
{
    public async Task AgregarAsync(Envio envio, CancellationToken ct) =>
        await db.Envios.AddAsync(envio, ct);

    public Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Envios
            .Include(e => e.Bultos)
            .Include(e => e.Intentos)
                .ThenInclude(i => i.ResultadosBultos)
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
            .Where(e =>
                e.OperadorId == operadorId &&
                (e.Estado == EstadoEnvio.EnDeposito ||
                e.Estado == EstadoEnvio.Reprogramado ||
                e.Estado == EstadoEnvio.EnDevolucion) &&
                !db.Set<ParadaRuta>().Any(p =>
                    p.EnvioId == e.Id &&
                    p.LiberadaEn == null))
            .OrderByDescending(e => e.FechaAlta)
            .ToListAsync(ct);
    }

    public Task<Envio?> ObtenerPorTokenSeguimientoAsync(
    string tokenHash,
    CancellationToken ct)
{
    return db.Envios
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Include(e => e.Eventos)
        .Include(e => e.Bultos)
        .AsSplitQuery()
        .FirstOrDefaultAsync(
            e => e.TokenSeguimientoHash == tokenHash,
            ct);
}

    public async Task<IReadOnlyList<Envio>> ListarPorCuentasComercialesAsync(
        IReadOnlyCollection<Guid> cuentasComercialesIds,
        CancellationToken ct)
    {
        if (cuentasComercialesIds.Count == 0)
            return Array.Empty<Envio>();

        return await db.Envios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(e => e.Bultos)
            .Where(e =>
                cuentasComercialesIds.Contains(e.CuentaComercialId))
            .OrderByDescending(e => e.FechaAlta)
            .ToListAsync(ct);
    }

        public Task<Envio?> ObtenerPorComercioAsync(
        Guid envioId,
        Guid comercioId,
        CancellationToken ct)
    {
        return db.Envios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(e => e.Bultos)
            .Include(e => e.Intentos)
                .ThenInclude(i => i.ResultadosBultos)
            .Include(e => e.Eventos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e =>
                e.Id == envioId &&
                db.CuentasComerciales
                    .IgnoreQueryFilters()
                    .Any(c =>
                        c.Id == e.CuentaComercialId &&
                        c.OperadorId == e.OperadorId &&
                        c.ComercioId == comercioId &&
                        c.Activa &&
                        c.Estado == EstadoCuentaComercial.Confirmada),
                ct);
    }
}
