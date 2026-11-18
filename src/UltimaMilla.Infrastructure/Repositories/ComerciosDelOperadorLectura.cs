using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.CuentasComerciales.Gestion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class ComerciosDelOperadorLectura(AppDbContext db) : IComerciosDelOperadorLectura
{
    public async Task<IReadOnlyList<ComercioDelOperadorDto>> ListarAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        var filas = await (
            from c in db.CuentasComerciales.AsNoTracking()
            join m in db.Comercios.AsNoTracking() on c.ComercioId equals m.Id
            where c.OperadorId == operadorId
            orderby m.Nombre
            select new { Cuenta = c, Comercio = m }
        ).ToListAsync(ct);

        return filas
            .Select(f => new ComercioDelOperadorDto(
                f.Cuenta.Id,
                f.Comercio.Id,
                f.Comercio.Nombre,
                f.Comercio.Rut,
                f.Cuenta.Estado,
                f.Cuenta.Activa,
                f.Cuenta.DireccionRecepcion?.Calle,
                f.Cuenta.DireccionRecepcion?.Numero,
                f.Cuenta.DireccionRecepcion?.Localidad,
                f.Cuenta.DireccionRecepcion?.Departamento))
            .ToList();
    }
}
