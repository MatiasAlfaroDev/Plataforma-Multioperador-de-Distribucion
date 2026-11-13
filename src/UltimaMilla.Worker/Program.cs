using MassTransit;
using StackExchange.Redis;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UltimaMilla.Application;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Correo;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Infrastructure.Caching;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Infrastructure;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Observabilidad;
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
    x.AddConsumer<EnviarEnlaceSeguimientoConsumer, EnviarEnlaceSeguimientoDefinition>();
    x.AddConsumer<InvalidarSeguimientoConsumer, InvalidarSeguimientoDefinition>();
    x.AddConsumer<AvisarAsignacionConsumer, AvisarAsignacionDefinition>();
});
builder.Services.AddSingleton<IEnviadorCorreo, SmtpEnviadorCorreo>();

// Un TenantDelMensaje por mensaje; AppDbContext lo recibe como ITenantContext.
builder.Services.AddScoped<TenantDelMensaje>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantDelMensaje>());

// Tareas programadas (RNF 6.13): solo en el Worker, con lock en Redis para que no corran dos veces.
// Sin Redis el Worker arranca igual: la caché falla en silencio y el lock reintenta en la vuelta siguiente.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var opciones = ConfigurationOptions.Parse(redis);
    opciones.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(opciones);
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddHostedService<ReintentoDeAvisos>();

// Apagado controlado: los mensajes en curso terminan antes de cerrar; los que no llegan, RabbitMQ los reentrega.
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

// Telemetría (RNF 6.12): la traza que empieza en la API sigue por MassTransit hasta el consumidor y el POST al comercio.
builder.Services.AddRegistroEstructurado();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddAttributes([new("service.instance.id", Environment.MachineName)]))
    .WithTracing(t => t.AddSource("MassTransit").AddSource("Npgsql").AddHttpClientInstrumentation())
    .WithMetrics(m => m.AddMeter("MassTransit").AddMeter(AvisosMetrics.MeterName).AddMeter(CacheMetrics.MeterName).AddMeter(SincronizacionMetrics.MeterName).AddMeter("System.Runtime").AddHttpClientInstrumentation())
    .UseOtlpExporter();

var host = builder.Build();
SincronizacionMetrics.Observar(host.Services.GetRequiredService<IConnectionMultiplexer>());
host.Run();
