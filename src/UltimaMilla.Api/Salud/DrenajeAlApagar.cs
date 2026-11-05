using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace UltimaMilla.Api.Salud;

/// <summary>
/// Apagado controlado (RNF 6.13). Al llegar el SIGTERM se dispara ApplicationStopping y /ready empieza a fallar,
/// pero Kestrel sigue atendiendo durante <see cref="Espera"/>: en ese tiempo Traefik (chequeo cada 5 s) saca la
/// instancia del pool. Recién después el servidor deja de aceptar conexiones y termina las que tiene en curso.
/// </summary>
public sealed class DrenajeAlApagar(IHostApplicationLifetime vida) : IHealthCheck, IHostedLifecycleService
{
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        return vida.ApplicationStopping.IsCancellationRequested
            ? Task.FromResult(HealthCheckResult.Unhealthy("La instancia se está apagando."))
            : Task.FromResult(HealthCheckResult.Healthy());
    }

    public Task StoppingAsync(CancellationToken ct) => Task.Delay(Espera, ct);

    public Task StartingAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken ct) => Task.CompletedTask;
}
