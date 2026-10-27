
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Infrastructure.Persistence.Interceptors;

public sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private static void Validar(DbContext? context)
    {
        if (context is not AppDbContext db)
            return;

        // El inicializador tiene un contexto interno explícito.
        if (db.AccesoInicializacion)
            return;

        var entidades = db.ChangeTracker
            .Entries()
            .Where(e =>
                e.Entity is ITenantEntity &&
                e.State is EntityState.Added
                    or EntityState.Modified
                    or EntityState.Deleted)
            .ToList();

        if (entidades.Count == 0)
            return;

        var operadorId = db.OperadorActualId;

        if (!operadorId.HasValue)
            throw new UnauthorizedAccessException(
                "Debe seleccionar un operador antes de guardar datos.");

        foreach (var entry in entidades)
        {
            var entidad = (ITenantEntity)entry.Entity;

            if (entidad.OperadorId != operadorId.Value)
                throw new UnauthorizedAccessException(
                    "No se permite modificar datos de otro operador.");

            if (entry.State == EntityState.Modified &&
                entry.Property(nameof(ITenantEntity.OperadorId)).IsModified)
                throw new InvalidOperationException(
                    "No se permite cambiar el operador de una entidad.");
        }
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Validar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Validar(eventData.Context);
        return base.SavingChangesAsync(
            eventData, result, cancellationToken);
    }
}
