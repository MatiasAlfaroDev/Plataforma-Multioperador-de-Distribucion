using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Infrastructure.Persistence.Interceptors;

public sealed class TenantSaveChangesInterceptor(
    ITenantContext tenant) : SaveChangesInterceptor
{
    private void Validar(DbContext? context)
    {
        if (context is not AppDbContext db)
            return;

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

        foreach (var entry in entidades)
        {
            var entidad = (ITenantEntity)entry.Entity;

            // Única excepción: crear una solicitud pendiente
            // del comercio autenticado.
            if (entry.State == EntityState.Added &&
                entry.Entity is CuentaComercial cuenta &&
                tenant.EstaAutenticado &&
                tenant.ComercioId.HasValue &&
                tenant.Perfil == Perfiles.AdministradorComercio &&
                cuenta.ComercioId == tenant.ComercioId.Value &&
                cuenta.Estado == EstadoCuentaComercial.Pendiente &&
                !cuenta.Activa)
            {
                continue;
            }

            // Todas las demás operaciones requieren operador.
            if (!operadorId.HasValue)
                throw new UnauthorizedAccessException(
                    "Debe seleccionar un operador antes de guardar datos.");

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