using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
