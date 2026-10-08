using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Protege las escrituras de entidades pertenecientes a un tenant.
/// Impide crear, modificar o eliminar datos de un operador distinto
/// al operador del contexto actual.
/// </summary>
public sealed class TenantEntityInterceptor(
    ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ValidarTenant(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidarTenant(eventData.Context);

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private void ValidarTenant(DbContext? context)
    {
        if (context is null)
            return;

        var entradasTenant = context.ChangeTracker
            .Entries<ITenantEntity>()
            .Where(entry =>
                entry.State is EntityState.Added
                    or EntityState.Modified
                    or EntityState.Deleted)
            .ToList();

        // Si solamente se están guardando entidades globales,
        // no necesitamos un tenant activo.
        if (entradasTenant.Count == 0)
            return;

        var operadorId = tenantContext.OperadorId;

        if (!operadorId.HasValue)
        {
            throw new InvalidOperationException(
                "No se puede guardar una entidad de tenant sin un operador activo.");
        }

        foreach (var entry in entradasTenant)
        {
            if (entry.Entity.OperadorId != operadorId.Value)
            {
                throw new InvalidOperationException(
                    "No se puede modificar información perteneciente a otro operador.");
            }
        }
    }
}