using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class ComercioRepository(AppDbContext db) : IComercioRepository
{
    public Task<Comercio?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Comercios.FirstOrDefaultAsync(c => c.Id == id, ct);
}
