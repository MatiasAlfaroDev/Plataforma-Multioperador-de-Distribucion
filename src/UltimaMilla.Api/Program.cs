using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

var app = builder.Build();

await app.Services.InicializarBaseDeDatosAsync();

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

// Permite usar Program en las pruebas de integración más adelante.
public partial class Program { }
