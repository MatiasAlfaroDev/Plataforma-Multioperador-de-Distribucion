using MassTransit;
using StackExchange.Redis;
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
using UltimaMilla.Worker.Tareas;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default.");

var rabbitMq = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:RabbitMq.");

var redis = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Redis.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, rabbitMq, x =>
{
    x.AddConsumer<GenerarAvisosConsumer, GenerarAvisosDefinition>();
    x.AddConsumer<EntregarAvisoConsumer, EntregarAvisoDefinition>();
});

// Un TenantDelMensaje por mensaje; AppDbContext lo recibe como ITenantContext.
builder.Services.AddScoped<TenantDelMensaje>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantDelMensaje>());

// Tareas programadas (RNF 6.13): solo en el Worker, con lock en Redis para que no corran dos veces.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
builder.Services.AddHostedService<ReintentoDeAvisos>();

// Apagado controlado: los mensajes en curso terminan antes de cerrar; los que no llegan, RabbitMQ los reentrega.
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

// Telemetría (RNF 6.12): la traza que empieza en la API sigue por MassTransit hasta el consumidor y el POST al comercio.
builder.Logging.AddOpenTelemetry(o => o.IncludeFormattedMessage = true);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddAttributes([new("service.instance.id", Environment.MachineName)]))
    .WithTracing(t => t.AddSource("MassTransit").AddSource("Npgsql").AddHttpClientInstrumentation())
    .WithMetrics(m => m.AddMeter("MassTransit").AddMeter(AvisosMetrics.MeterName).AddMeter("System.Runtime").AddHttpClientInstrumentation())
    .UseOtlpExporter();

builder.Build().Run();
