using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using UltimaMilla.Api.Errores;
using UltimaMilla.Api.Salud;
using UltimaMilla.Application;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Application.Envios.Queries.ListarEnvios;
using UltimaMilla.Application.Operadores.Queries.ListarOperadores;
using UltimaMilla.Infrastructure;
using UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;
using UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;
using UltimaMilla.Application.Configuracion.Queries.ListarZonas;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorDeErrores>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck<BaseDeDatosHealthCheck>("base-de-datos", tags: ["ready"]);

// Telemetría (RNF 6.12): logs, métricas y trazas salen por OTLP al Collector, y es él el que
// decide el backend (docker/otel-collector-config.yaml). El destino y el nombre del servicio
// los pone el compose: OTEL_EXPORTER_OTLP_ENDPOINT y OTEL_SERVICE_NAME.
builder.Logging.AddOpenTelemetry(o => o.IncludeFormattedMessage = true);
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        // Sin el filtro, el health check de Traefik cada 5s por réplica aporta dos spans
        // (/ready y su SELECT 1) y tapa las trazas de negocio en el dashboard.
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !EsSonda(ctx.Request.Path))
        // Npgsql 10 trae su propio ActivitySource, así que cada consulta es una span sin
        // agregar paquetes; el de EF Core solo existe en beta.
        .AddSource("Npgsql"))
    .WithMetrics(m => m.AddAspNetCoreInstrumentation())
    .UseOtlpExporter();

var app = builder.Build();

// Un solo proceso prepara la base. El servicio `inicializador` del compose corre con esta
// variable en true y termina; las réplicas de la API no inicializan nada, porque dos
// sembrando a la vez contra una base vacía violan el índice único. Acá entra MigrateAsync (15/10).
if (builder.Configuration.GetValue<bool>("Inicializacion:SoloInicializar"))
{
    await app.Services.InicializarBaseDeDatosAsync();
    return;
}

app.UseExceptionHandler();
app.MapOpenApi();   // contrato en /openapi/v1.json (Scalar se agrega con la API pública)

// Salud (RNF 6.13): /live = el proceso responde; /ready = además llega a la base; /health = todo.
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false });

var api = app.MapGroup("/api");

api.MapGet("/operadores", async (ListarOperadoresHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarOperadoresQuery(), ct)));

// Provisorio hasta el login del 15/10: el Portal elige con qué cuenta comercial operar.
api.MapGet("/cuentas-comerciales", async (ListarCuentasComercialesHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarCuentasComercialesQuery(), ct)));

// POST /api/envios → alta de envío desde el Portal del comercio.
api.MapPost("/envios", async (CrearEnvioCommand command, CrearEnvioHandler handler, CancellationToken ct) =>
{
    var id = await handler.Handle(command, ct);
    return Results.Created($"/api/envios/{id}", new { id });
});

// GET /api/envios?operadorId=... → listado para el Backoffice.
api.MapGet("/envios", async (Guid? operadorId, ListarEnviosHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarEnviosQuery(operadorId), ct)));

api.MapPost("/tarifas", async (ActualizarTarifaCommand command, ActualizarTarifaHandler handler, CancellationToken ct) =>
{
    var id = await handler.Handle(command, ct);
    return Results.Ok(new { id });
});
api.MapGet("/tarifas/vigente", async (Guid operadorId, Guid zonaCoberturaId, ObtenerTarifaVigenteHandler handler,CancellationToken ct) =>
{
    var tarifa = await handler.Handle(
        new ObtenerTarifaVigenteQuery(operadorId, zonaCoberturaId),
        ct);

    return tarifa is null
        ? Results.NotFound()
        : Results.Ok(tarifa);
});
api.MapGet("/zonas", async (Guid operadorId, ListarZonasHandler handler, CancellationToken ct) =>
{
    var zonas = await handler.Handle(
        new ListarZonasQuery(operadorId),
        ct);

    return Results.Ok(zonas);
});

app.Run();

// Las sondas de salud no se instrumentan: al descartarse la span del request, la consulta
// de Npgsql queda sin padre grabado y el muestreo la descarta también.
static bool EsSonda(PathString ruta) =>
    ruta.StartsWithSegments("/health") || ruta.StartsWithSegments("/ready") || ruta.StartsWithSegments("/live");

// Permite usar Program en las pruebas de integración más adelante.
public partial class Program { }
