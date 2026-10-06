using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class CuentaComercialRepository(AppDbContext db) : ICuentaComercialRepository
{
    public Task<CuentaComercial?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.CuentasComerciales.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(CancellationToken ct) =>
        await (from c in db.CuentasComerciales
               join o in db.Operadores on c.OperadorId equals o.Id
               join m in db.Comercios on c.ComercioId equals m.Id
               orderby m.Nombre, o.Nombre
               select new CuentaComercialDto(c.Id, o.Id, o.Nombre, m.Id, m.Nombre, c.Activa))
              .AsNoTracking()
              .ToListAsync(ct);
}
