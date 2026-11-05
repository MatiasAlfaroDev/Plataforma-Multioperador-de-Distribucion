using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class OperadorRepository(AppDbContext db) : IOperadorRepository
{
    // Lista todos los operadores.
    public async Task<IReadOnlyList<Operador>> ListarAsync(CancellationToken ct) =>
        await db.Operadores
            .AsNoTracking()
            .OrderBy(o => o.Nombre)
            .ToListAsync(ct);

    // Busca un operador por su ID.
    public Task<Operador?> ObtenerAsync(
        Guid id,
        CancellationToken ct)
    {
        return db.Operadores
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }
}