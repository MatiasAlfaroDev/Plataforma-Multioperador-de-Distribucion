using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Domain.Envios;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace UltimaMilla.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Publica un EstadoEnvioCambiado por cada EventoEnvio agregado, dentro de la misma transacción,
/// para que ningún Handler tenga que acordarse de publicarlos (RF 28). Cada transición de Envio.Aplicar
/// deja un EventoEnvio y este interceptor los publica en el outbox (RNF 6.8).
/// </summary>
public sealed class EventosDeEstadoInterceptor(IServiceProvider servicios) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            var entries = eventData.Context.ChangeTracker.Entries<EventoEnvio>()
                .Where(e => e.State == EntityState.Added)
                .ToList();

            if (entries.Count > 0)
                throw new InvalidOperationException("Usar SaveChangesAsync: publicar en el outbox es asíncrono.");
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var context = eventData.Context;

        context.ChangeTracker.DetectChanges();

        var entries = context.ChangeTracker.Entries<EventoEnvio>()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        if (entries.Count > 0)
        {
            var publicador = servicios.GetRequiredService<IPublishEndpoint>();

            foreach (var entry in entries)
            {
                var e = entry.Entity;
                await publicador.Publish(new EstadoEnvioCambiado(
                    e.Id,
                    e.EnvioId,
                    e.OperadorId,
                    e.EstadoAnterior,
                    e.EstadoNuevo,
                    e.OcurridoEn),
                    cancellationToken);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
