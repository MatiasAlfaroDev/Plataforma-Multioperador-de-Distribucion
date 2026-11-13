using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace UltimaMilla.Infrastructure.Observabilidad;

public static class RegistroEstructurado
{
    /// <summary>
    /// Serilog (RNF 6.12) para la API y el Worker: consola y OTLP al Collector, que lo lleva a Loki con el
    /// trace id de cada línea. El destino y el nombre del servicio salen de OTEL_EXPORTER_OTLP_ENDPOINT y
    /// OTEL_SERVICE_NAME, las mismas variables que usan las trazas.
    /// </summary>
    public static IServiceCollection AddRegistroEstructurado(this IServiceCollection services) =>
        services.AddSerilog(cfg => cfg
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.OpenTelemetry(o => o.ResourceAttributes["service.instance.id"] = Environment.MachineName));
}
