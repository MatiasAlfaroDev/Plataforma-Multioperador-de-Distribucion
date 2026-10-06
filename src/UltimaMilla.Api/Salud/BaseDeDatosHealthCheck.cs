using Microsoft.Extensions.Diagnostics.HealthChecks;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Salud;

/// <summary>La API está "lista" solo si puede hablar con PostgreSQL. Traefik usa /ready para el balanceo.</summary>
public sealed class BaseDeDatosHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy("PostgreSQL responde.")
            : HealthCheckResult.Unhealthy("No hay conexión con PostgreSQL.");
}
