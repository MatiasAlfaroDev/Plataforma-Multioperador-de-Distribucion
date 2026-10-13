using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Interceptors;

public sealed class TenantEntityInterceptor(
    ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ValidarOperador(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await ValidarTenantAsync(eventData.Context, cancellationToken);

        return await base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private static List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ITenantEntity>>
        ObtenerEntradas(DbContext context) =>
        context.ChangeTracker
            .Entries<ITenantEntity>()
            .Where(e => e.State is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted)
            .ToList();

    private void ValidarOperador(DbContext? context)
    {
        if (context is null)
            return;

        var entradas = ObtenerEntradas(context);

        if (entradas.Count == 0)
            return;

        var operadorId = tenantContext.OperadorId;

        if (!operadorId.HasValue || tenantContext.ComercioId.HasValue)
            throw new InvalidOperationException(
                "El guardado sincrónico requiere un contexto de operador.");

        foreach (var entrada in entradas)
        {
            if (entrada.Entity.OperadorId != operadorId.Value)
                throw new InvalidOperationException(
                    "No se puede modificar información de otro operador.");
        }
    }

    private async Task ValidarTenantAsync(
        DbContext? context,
        CancellationToken ct)
    {
        if (context is null)
            return;

        var entradas = ObtenerEntradas(context);

        if (entradas.Count == 0)
            return;

        var operadorId = tenantContext.OperadorId;
        var comercioId = tenantContext.ComercioId;

        if (operadorId.HasValue && !comercioId.HasValue)
        {
            foreach (var entrada in entradas)
            {
                if (entrada.Entity.OperadorId != operadorId.Value)
                    throw new InvalidOperationException(
                        "No se puede modificar información de otro operador.");
            }

            return;
        }

        if (comercioId.HasValue && !operadorId.HasValue)
        {
            foreach (var entrada in entradas)
            {
                if (entrada.Entity is not Envio envio)
                    throw new InvalidOperationException(
                        "El comercio no puede modificar esta entidad.");

                // Solo permitimos altas de envíos desde el comercio
                // hasta definir reglas específicas para otras operaciones.
                if (entrada.State != EntityState.Added)
                    throw new InvalidOperationException(
                        "El comercio no puede modificar ni eliminar envíos mediante esta operación.");

                var cuentaAutorizada = await context
                    .Set<UltimaMilla.Domain.Organizacion.CuentaComercial>()
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == envio.CuentaComercialId &&
                        c.ComercioId == comercioId.Value &&
                        c.OperadorId == envio.OperadorId &&
                        c.Activa,
                        ct);

                if (!cuentaAutorizada)
                    throw new InvalidOperationException(
                        "La cuenta comercial no está autorizada para este envío.");
            }

            return;
        }

        throw new InvalidOperationException(
            "No existe un contexto de tenant válido para guardar.");
    }
}