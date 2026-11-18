
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class OperadorRepository(AppDbContext db)
    : IOperadorRepository
{
    public async Task<IReadOnlyList<Operador>> ListarAsync(
        CancellationToken ct) =>
        await db.Operadores
            .AsNoTracking()
            .OrderBy(o => o.Nombre)
            .ToListAsync(ct);

    public Task<Operador?> ObtenerAsync(
        Guid id,
        CancellationToken ct)
    {
        return db.Operadores
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public Task<Operador?> ObtenerParaActualizarAsync(
        Guid id,
        CancellationToken ct)
    {
        return db.Operadores
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }
}
