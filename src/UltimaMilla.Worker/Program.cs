using MassTransit;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UltimaMilla.Application;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Infrastructure;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Worker;
using UltimaMilla.Worker.Consumidores;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default.");

var rabbitMq = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:RabbitMq.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, rabbitMq, x =>
{
    x.AddConsumer<GenerarAvisosConsumer, GenerarAvisosDefinition>();
    x.AddConsumer<EntregarAvisoConsumer, EntregarAvisoDefinition>();
});

// Un TenantDelMensaje por mensaje; AppDbContext lo recibe como ITenantContext.
builder.Services.AddScoped<TenantDelMensaje>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantDelMensaje>());

// Telemetría (RNF 6.12): la traza que empieza en la API sigue por MassTransit hasta el consumidor y el POST al comercio.
builder.Logging.AddOpenTelemetry(o => o.IncludeFormattedMessage = true);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddAttributes([new("service.instance.id", Environment.MachineName)]))
    .WithTracing(t => t.AddSource("MassTransit").AddSource("Npgsql").AddHttpClientInstrumentation())
    .WithMetrics(m => m.AddMeter("MassTransit").AddMeter(AvisosMetrics.MeterName).AddMeter("System.Runtime").AddHttpClientInstrumentation())
    .UseOtlpExporter();

builder.Build().Run();
