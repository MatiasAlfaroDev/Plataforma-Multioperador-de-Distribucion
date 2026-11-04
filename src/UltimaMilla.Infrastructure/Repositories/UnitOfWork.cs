
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> GuardarCambiosAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public async Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> operacion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        await using var transaccion =
            await db.Database.BeginTransactionAsync(ct);

        try
        {
            await operacion(ct);
            await transaccion.CommitAsync(ct);
        }
        catch
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
