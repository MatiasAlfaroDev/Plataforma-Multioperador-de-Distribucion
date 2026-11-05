using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class CuentaComercialRepository(AppDbContext db)
    : ICuentaComercialRepository
{
    public Task<CuentaComercial?> ObtenerAsync(
        Guid id,
        CancellationToken ct) =>
        db.CuentasComerciales
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct)
    {
        var consulta = db.CuentasComerciales
            .AsNoTracking()
            .Where(c => c.OperadorId == operadorId);

        if (cuentaComercialId.HasValue)
        {
            consulta = consulta.Where(
                c => c.Id == cuentaComercialId.Value);
        }

        return await (
            from c in consulta
            join o in db.Operadores
                on c.OperadorId equals o.Id
            join m in db.Comercios
                on c.ComercioId equals m.Id
            orderby m.Nombre, o.Nombre
            select new CuentaComercialDto(
                c.Id,
                o.Id,
                o.Nombre,
                m.Id,
                m.Nombre,
                c.Activa)
        ).ToListAsync(ct);
    }

        public Task<CuentaComercial?> ObtenerPorOperadorYComercioAsync(
        Guid operadorId,
        Guid comercioId,
        CancellationToken ct)
    {
        return db.CuentasComerciales
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.OperadorId == operadorId &&
                c.ComercioId == comercioId,
                ct);
    }

    public async Task AgregarAsync(
        CuentaComercial cuenta,
        CancellationToken ct)
    {
        await db.CuentasComerciales.AddAsync(cuenta, ct);
    }

    public async Task<IReadOnlyList<SolicitudPendienteDto>>
    ListarSolicitudesPendientesAsync(
        Guid operadorId,
        CancellationToken ct)
{
    return await (
        from cuenta in db.CuentasComerciales.AsNoTracking()
        join comercio in db.Comercios.AsNoTracking()
            on cuenta.ComercioId equals comercio.Id
        where cuenta.OperadorId == operadorId
              && cuenta.Estado == EstadoCuentaComercial.Pendiente
        orderby cuenta.CreadoEn descending
        select new SolicitudPendienteDto(
            cuenta.Id,
            comercio.Id,
            comercio.Nombre,
            cuenta.CreadoEn)
    ).ToListAsync(ct);
}

public async Task<IReadOnlyList<CuentaComercialDto>> ListarPorComercioAsync(
    Guid comercioId,
    CancellationToken ct)
{
    return await (
        from c in db.CuentasComerciales
            .IgnoreQueryFilters()
            .AsNoTracking()
        where c.ComercioId == comercioId
        join o in db.Operadores.AsNoTracking()
            on c.OperadorId equals o.Id
        join m in db.Comercios.AsNoTracking()
            on c.ComercioId equals m.Id
        orderby o.Nombre
        select new CuentaComercialDto(
            c.Id,
            c.OperadorId,
            o.Nombre,
            c.ComercioId,
            m.Nombre,
            c.Activa
        )
    ).ToListAsync(ct);
}
}