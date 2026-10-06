using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Completa CreadoEn y ActualizadoEn antes de guardar, como el interceptor de la demo Support.
/// Para el 15/10 un interceptor similar asignará y validará el OperadorId (ADR-002).
/// </summary>
public sealed class AuditableEntityInterceptor(TimeProvider reloj) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Auditar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Auditar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Auditar(DbContext? context)
    {
        if (context is null) return;

        var ahora = reloj.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.MarcarCreado(ahora);
            else if (entry.State == EntityState.Modified)
                entry.Entity.MarcarActualizado(ahora);
        }
    }
}
