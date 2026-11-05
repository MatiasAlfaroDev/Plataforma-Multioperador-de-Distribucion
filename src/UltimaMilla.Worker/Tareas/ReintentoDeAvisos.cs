using StackExchange.Redis;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Commands.ReintentarAvisosFallidos;

namespace UltimaMilla.Worker.Tareas;

/// <summary>
/// Tarea programada que cada minuto reencola los avisos fallidos cuya espera creciente venció (RNF 6.9 y escenario 6: se detiene el receptor, se reactiva y los avisos se recuperan solos).
/// Vive solo en el Worker (informe 6: las tareas programadas no se alojan en la API escalada) y el lock en Redis cubre el caso de dos réplicas o un reinicio solapado.
/// </summary>
public sealed class ReintentoDeAvisos(IServiceScopeFactory scopes, IConnectionMultiplexer redis, TimeProvider reloj, ILogger<ReintentoDeAvisos> logger) : BackgroundService
{
    private static readonly TimeSpan Periodo = TimeSpan.FromMinutes(1);
    private const string ClaveLock = "ultimamilla:lock:reintento-avisos";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Periodo, reloj);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await EjecutarVueltaAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falló la vuelta del reintento de avisos.");
            }
        }
    }

    private async Task EjecutarVueltaAsync(CancellationToken ct)
    {
        // Lock distribuido: con dos réplicas del Worker la tarea no corre dos veces.
        var tomado = await redis.GetDatabase().StringSetAsync(ClaveLock, Environment.MachineName, Periodo - TimeSpan.FromSeconds(5), When.NotExists);
        if (!tomado)
        {
            logger.LogDebug("Otra réplica tiene el lock del reintento de avisos.");
            return;
        }

        // NO liberar el lock al terminar: que expire solo, así ninguna otra réplica repite la vuelta en el mismo minuto.
        await using var scope = scopes.CreateAsyncScope();
        var operadores = await scope.ServiceProvider.GetRequiredService<IEntregaAvisoRepository>().OperadoresConFallidasAsync(ct);

        foreach (var operadorId in operadores)
        {
            // Un scope nuevo por operador: AppDbContext y TenantDelMensaje.
            await using var scopeOperador = scopes.CreateAsyncScope();
            scopeOperador.ServiceProvider.GetRequiredService<TenantDelMensaje>().Fijar(operadorId);

            // Hay que fijar el operador ANTES del Handler (si no, los filtros por operador no devuelven nada y TenantSaveChangesInterceptor rechaza el guardado).
            var n = await scopeOperador.ServiceProvider.GetRequiredService<ReintentarAvisosFallidosHandler>().Handle(new ReintentarAvisosFallidosCommand(), ct);
            if (n > 0)
                logger.LogInformation("Reencolados {Cantidad} avisos fallidos del operador {OperadorId}.", n, operadorId);
        }
    }
}
