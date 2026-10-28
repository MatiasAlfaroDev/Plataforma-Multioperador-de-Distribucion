using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using UltimaMilla.Api.Errores;
using UltimaMilla.Api.Salud;
using UltimaMilla.Application;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;
using UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;
using UltimaMilla.Application.Envios.Queries.ListarEnvios;
using UltimaMilla.Application.Operadores.Queries.ListarOperadores;
using UltimaMilla.Infrastructure;
using UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;
using UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;
using UltimaMilla.Application.Configuracion.Queries.ListarZonas;
using UltimaMilla.Application.Configuracion.Commands.CrearZona;
using UltimaMilla.Application.Configuracion.Commands.EditarZona;
using UltimaMilla.Application.Configuracion.Queries.ObtenerZona;
using UltimaMilla.Application.Rutas.Commands.CrearHojaDeRuta;
using UltimaMilla.Application.Rutas.Queries.ConsultarHojasDeRuta;
using UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using UltimaMilla.Infrastructure.Identidad;
using UltimaMilla.Api.Endpoints;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Api.Seguridad;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Application.Seguridad;
using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default.");

var rabbitMq = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:RabbitMq.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, rabbitMq);

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "UltimaMilla.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

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
    // Las réplicas comparten OTEL_SERVICE_NAME; sin un id de instancia propio (el hostname del contenedor)
    // Prometheus suma sus series como si fueran una.
    .ConfigureResource(r => r.AddAttributes([new("service.instance.id", Environment.MachineName)]))
    .WithTracing(t => t
        // Sin el filtro, el health check de Traefik cada 5s por réplica aporta dos spans
        // (/ready y su SELECT 1) y tapa las trazas de negocio en el dashboard.
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !EsSonda(ctx.Request.Path))
        // Npgsql 10 trae su propio ActivitySource, así que cada consulta es una span sin
        // agregar paquetes; el de EF Core solo existe en beta.
        .AddSource("Npgsql")
        // la traza del POST sigue hasta la publicación del outbox y la entrega a RabbitMQ
        .AddSource("MassTransit"))
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation();
        m.AddMeter("MassTransit");
        // .NET 10 emite las métricas de runtime (GC, hilos, memoria) en el medidor System.Runtime sin paquete extra.
        m.AddMeter("System.Runtime");
    })
    .UseOtlpExporter();
builder.Services.AddScoped<CrearZonaHandler>();
var soloInicializar = builder.Configuration.GetValue<bool>(
    "Inicializacion:SoloInicializar");

if (!soloInicializar)
{
    var redisConnectionString =
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException(
            "No se configuró ConnectionStrings:Redis.");

    var redis = await ConnectionMultiplexer.ConnectAsync(
        redisConnectionString);

    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

    builder.Services
        .AddDataProtection()
        .SetApplicationName("UltimaMilla")
        .PersistKeysToStackExchangeRedis(
            redis,
            "UltimaMilla:DataProtection:Keys");
}

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapOpenApi();  // contrato en /openapi/v1.json (Scalar se agrega con la API pública)

// Salud (RNF 6.13): /live = el proceso responde; /ready = además llega a la base; /health = todo.
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false });
app.MapAutenticacionEndpoints();

app.MapGet("/api/antiforgery/token", (
    HttpContext context,
    IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);

    context.Response.Headers.CacheControl = "no-store";

    return Results.Ok(new
    {
        token = tokens.RequestToken
    });
})
.RequireAuthorization();

var api = app.MapGroup("/api");

api.MapGet("/operadores", async (ListarOperadoresHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarOperadoresQuery(), ct)));

// Provisorio hasta el login del 15/10: el Portal elige con qué cuenta comercial operar.
api.MapGet("/cuentas-comerciales", async (
    ListarCuentasComercialesHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(
        new ListarCuentasComercialesQuery(), ct)))
    .RequireAuthorization(policy =>
        policy.RequireRole(
            "Comercio",
            "AdministradorOperador",
            "Operador"));

// POST /api/envios → alta de envío desde el Portal del comercio.
api.MapPost("/envios", async (
    HttpContext context,
    IAntiforgery antiforgery,
    CrearEnvioCommand command,
    CrearEnvioHandler handler,
    CancellationToken ct) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Token de seguridad inválido",
            detail: "La solicitud no contiene un token antifalsificación válido.");
    }

    var id = await handler.Handle(command, ct);

    return Results.Created(
        $"/api/envios/{id}",
        new { id });
})
.RequireAuthorization(policy => policy.RequireRole(
    Perfiles.AdministradorOperador,
    Perfiles.Operador,
    Perfiles.Comercio));
// GET /api/envios?operadorId=... → listado para el Backoffice.
// GET /api/envios → listado según el operador y perfil autenticado.
api.MapGet("/envios", async (
    ListarEnviosHandler handler,
    CancellationToken ct) =>
{
    var resultado = await handler.Handle(
        new ListarEnviosQuery(null),
        ct);

    return Results.Ok(resultado);
})
.RequireAuthorization(policy => policy.RequireRole(
    Perfiles.AdministradorOperador,
    Perfiles.Operador,
    Perfiles.Comercio));

// POST /api/envios/{id}/eventos-movil → sincronización de la app del repartidor (entregado / no entregado).
// 201 = guardado; 409 = ya estaba (reintento, la app lo toma como éxito); 400 = conflicto o datos inválidos.
api.MapPost("/envios/{envioId:guid}/eventos-movil",
    async (Guid envioId, EventoMovilRequest request, RegistrarEventoMovilHandler handler, CancellationToken ct) =>
    {
        await handler.Handle(          
            new RegistrarEventoMovilCommand(
                envioId,
                request.IdEvento,
                request.Tipo,
                request.Motivo,
                request.Receptor,
                request.FechaHora,
                request.DocumentoReceptor,
                request.ReferenciaFirma,
                request.ReferenciaFotografia,
                request.Latitud,
                request.Longitud,
                request.EvidenciaFallo),ct);
        return Results.Created($"/api/envios/{envioId}/eventos-movil/{request.IdEvento}", new { id = request.IdEvento });
    });

api.MapPost("/envios/{envioId:guid}/recepcion",
    async (
        Guid envioId,
        RecibirEnDepositoRequest request,
        RecibirEnDepositoHandler handler,
        CancellationToken ct) =>
    {
        await handler.Handle(
            new RecibirEnDepositoCommand(
                envioId,
                request.IdentificacionesEscaneadas,
                request.Responsable),
            ct);

        return Results.NoContent();
    });

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


api.MapPost("/zonas", async (
    CrearZonaCommand command,
    CrearZonaHandler handler,
    CancellationToken ct) =>
{
    var id = await handler.Handle(command, ct);

    return Results.Ok(new { id });
});

// GET /api/zonas/{id}?operadorId=... → detalle de una zona, para el formulario de edición del Backoffice.
api.MapGet("/zonas/{zonaId:guid}", async (
    Guid zonaId,
    Guid operadorId,
    ObtenerZonaHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ObtenerZonaQuery(operadorId, zonaId), ct)))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorOperador));

// PUT /api/zonas/{id} → edición de una zona (RF 2). Configurar zonas es tarea del administrador del operador.
api.MapPut("/zonas/{zonaId:guid}", async (
    Guid zonaId,
    EditarZonaRequest request,
    EditarZonaHandler handler,
    CancellationToken ct) =>
{
    await handler.Handle(
        new EditarZonaCommand(
            request.OperadorId,
            zonaId,
            request.Nombre,
            request.Departamento,
            request.MinutosTraslado,
            request.Coberturas,
            request.Franjas),
        ct);

    return Results.NoContent();
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorOperador));

api.MapPost("/hojas-de-ruta", async (
    CrearHojaDeRutaCommand command,
    CrearHojaDeRutaHandler handler,
    CancellationToken ct) =>
{
    var id = await handler.Handle(command, ct);

    return Results.Created(
        $"/api/hojas-de-ruta/{id}",
        new { id });
});

api.MapGet("/hojas-de-ruta/opciones", async (
    Guid operadorId,
    ConsultarHojasDeRutaHandler handler,
    CancellationToken ct) =>
{
    var opciones = await handler.ObtenerOpcionesAsync(
        operadorId, ct);

    return Results.Ok(opciones);
});

api.MapGet("/hojas-de-ruta", async (
    Guid operadorId,
    ConsultarHojasDeRutaHandler handler,
    CancellationToken ct) =>
{
    var hojas = await handler.ListarAsync(operadorId, ct);

    return Results.Ok(hojas);
});

api.MapPost("/hojas-de-ruta/{hojaId:guid}/despachar",
    async (
        Guid hojaId,
        Guid operadorId,
        DespacharHojaDeRutaHandler handler,
        CancellationToken ct) =>
    {
        var cantidad = await handler.Handle(
            new DespacharHojaDeRutaCommand(
                operadorId,
                hojaId),
            ct);

        return Results.Ok(new
        {
            hojaDeRutaId = hojaId,
            enviosActualizados = cantidad
        });
    });


app.Run();

// Las sondas de salud no se instrumentan: al descartarse la span del request, la consulta
// de Npgsql queda sin padre grabado y el muestreo la descarta también.
static bool EsSonda(PathString ruta) =>
    ruta.StartsWithSegments("/health") || ruta.StartsWithSegments("/ready") || ruta.StartsWithSegments("/live");

// Permite usar Program en las pruebas de integración más adelante.
public partial class Program { }

/// <summary>Cuerpo que manda la app del repartidor; el envío viaja en la URL.</summary>

public sealed record EventoMovilRequest( Guid IdEvento,  string Tipo, string? Motivo, string? Receptor, DateTimeOffset FechaHora, string? DocumentoReceptor = null, string? ReferenciaFirma = null, string? ReferenciaFotografia = null, decimal? Latitud = null, decimal? Longitud = null, string? EvidenciaFallo = null);
public sealed record RecibirEnDepositoRequest( IReadOnlyList<string> IdentificacionesEscaneadas, string Responsable);

/// <summary>Cuerpo de PUT /api/zonas/{id}; la zona viaja en la URL.</summary>
public sealed record EditarZonaRequest(
    Guid OperadorId,
    string Nombre,
    string Departamento,
    int MinutosTraslado,
    IReadOnlyList<CoberturaCommand> Coberturas,
    IReadOnlyList<FranjaEntregaCommand> Franjas);
