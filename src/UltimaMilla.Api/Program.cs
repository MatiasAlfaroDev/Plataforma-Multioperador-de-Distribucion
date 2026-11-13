using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using UltimaMilla.Api.Errores;
using UltimaMilla.Api.Salud;
using UltimaMilla.Application;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Application.Envios.Commands.ImportarEnviosCsv;
using UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;
using UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;
using UltimaMilla.Application.Envios.Queries.ListarDiscrepanciasRecepcion;
using UltimaMilla.Application.Envios.Commands.Reprogramacion;
using UltimaMilla.Application.Envios.Queries.ListarSolicitudesReprogramacion;
using UltimaMilla.Application.Envios.Queries.ListarEnvios;
using UltimaMilla.Application.Operadores.Queries.ListarOperadores;
using UltimaMilla.Infrastructure;
using UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;
using UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;
using UltimaMilla.Application.Configuracion.Commands.ActualizarReglasOperativas;
using UltimaMilla.Application.Configuracion.Queries.ObtenerReglasOperativasVigentes;
using UltimaMilla.Application.Configuracion.Queries.ListarZonas;
using UltimaMilla.Application.Configuracion.Commands.CrearZona;
using UltimaMilla.Application.Configuracion.Commands.EditarZona;
using UltimaMilla.Application.Configuracion.Queries.ObtenerZona;
using UltimaMilla.Application.Rutas.Commands.RendirHojaDeRuta;
using UltimaMilla.Application.Rutas.Commands.CrearHojaDeRuta;
using UltimaMilla.Application.Rutas.Queries.ConsultarHojasDeRuta;
using UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;
using UltimaMilla.Application.Rutas.Commands.QuitarEnvioDeRuta;
using UltimaMilla.Application.Rutas.Commands.AgregarEnvioARuta;
using UltimaMilla.Application.Envios.Commands.RetornarADeposito;
using UltimaMilla.Application.Rutas.Queries.ObtenerMisHojasDeRuta;
using UltimaMilla.Application.Rutas.Commands.DespacharMiHojaDeRuta;
using UltimaMilla.Application.Rutas.Commands.RegistrarPosiciones;
using UltimaMilla.Application.Envios.Commands.DeclararExtravio;
using UltimaMilla.Application.Envios.Commands.RegistrarReaparicion;
using Microsoft.AspNetCore.Authentication;
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
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Infrastructure.Caching;
using UltimaMilla.Application.Notificaciones.Commands.RegistrarDispositivo;
using UltimaMilla.Application.Notificaciones.Commands.EnviarNotificacionDePrueba;
using UltimaMilla.Application.Envios.Queries.ConsultarSeguimientoPublico;
using UltimaMilla.Application.Avisos.Commands.CrearSuscripcion;
using UltimaMilla.Application.Avisos.Commands.ReenviarAviso;
using UltimaMilla.Application.Avisos.Queries.ListarAvisos;
using UltimaMilla.Application.Avisos.Queries.ListarSuscripciones;
using UltimaMilla.Application.Mensajeria.Commands.ReintentarMensajesFallidos;
using UltimaMilla.Application.Mensajeria.Queries.ListarMensajesFallidos;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Application.Reportes.Queries.ObtenerResumenEnvios;
using UltimaMilla.Application.Reportes.Queries.ObtenerMotivosNoEntrega;
using UltimaMilla.Application.Reportes.Queries.ObtenerActividadComercial;
using UltimaMilla.Application.Reportes.Queries.ObtenerResultadosRepartidores;
using UltimaMilla.Application.Reportes.Queries.ObtenerCumplimiento;
using UltimaMilla.Application.Reportes.Queries.ObtenerDetalleLiquidacion;
using UltimaMilla.Application.Reportes.Queries.ObtenerPuntualidad;
using UltimaMilla.Application.Envios.Commands.IniciarDevolucion;
using UltimaMilla.Application.Envios.Commands.ConfirmarDevolucion;
using UltimaMilla.Application.Envios.Queries.ObtenerDetalleEnvio;
using UltimaMilla.Application.Retiros.Commands.CrearRetiro;
using UltimaMilla.Application.Configuracion.Commands.CrearTarifa;
using UltimaMilla.Application.Retiros.Queries;
using UltimaMilla.Application.Rutas.Commands.AgregarRetiroARuta;
using UltimaMilla.Application.Retiros.Commands.GestionarRetiro;
using UltimaMilla.Application.Retiros.Commands.RegistrarEventoRetiro;
using UltimaMilla.Application.Reportes.Queries.ObtenerResumenRetiros;
using System.Security.Cryptography;
using UltimaMilla.Application.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default.");

var rabbitMq = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:RabbitMq.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, rabbitMq);

// Con X-Api-Key autentica la clave del comercio (RF 37); si no, la cookie de siempre.
const string CookieOClave = "CookieOClaveApi";

builder.Services
    .AddAuthentication(CookieOClave)
    .AddPolicyScheme(CookieOClave, CookieOClave, options =>
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(ClaveApiAuthenticationHandler.Encabezado)
                ? ClaveApiAuthenticationHandler.Esquema
                : IdentityConstants.ApplicationScheme)
    .AddScheme<AuthenticationSchemeOptions, ClaveApiAuthenticationHandler>(ClaveApiAuthenticationHandler.Esquema, null)
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

// Seguridad web (RNF 6.3): IP real detrás de Traefik, limitación de tasa, CORS y cabeceras. Ver Seguridad/SeguridadWeb.cs.
builder.Services.AddSeguridadWeb(builder.Configuration);

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorDeErrores>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck<BaseDeDatosHealthCheck>("base-de-datos", tags: ["ready"])
    .AddCheck<DrenajeAlApagar>("apagado", tags: ["ready"]);

// Apagado controlado (RNF 6.13): primero el drenaje de DrenajeAlApagar y después hasta 30 s para lo que esté en curso.
builder.Services.AddHostedService<DrenajeAlApagar>();
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));

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
        m.AddMeter("UltimaMilla.Cache");
        m.AddMeter("System.Runtime");
    })
    .UseOtlpExporter();
builder.Services.AddScoped<CrearZonaHandler>();
builder.Services.AddScoped<ConsultarSeguimientoPublicoHandler>();
var soloInicializar = builder.Configuration.GetValue<bool>(
    "Inicializacion:SoloInicializar");

if (!soloInicializar)
{
    var redisConnectionString =
        builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException(
            "No se configuró ConnectionStrings:Redis.");

    // Aclaración 3.1: sin Redis la API arranca igual y la caché se comporta como fallos (va a la base);
    // se reconecta sola cuando Redis vuelve.
    var opcionesRedis = ConfigurationOptions.Parse(redisConnectionString);
    opcionesRedis.AbortOnConnectFail = false;
    var redis = await ConnectionMultiplexer.ConnectAsync(opcionesRedis);

    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

    builder.Services
        .AddDataProtection()
        .SetApplicationName("UltimaMilla")
        .PersistKeysToStackExchangeRedis(
            redis,
            "UltimaMilla:DataProtection:Keys");
}

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<HttpTenantContext>();

builder.Services.AddScoped<ITenantContext>(
    sp => sp.GetRequiredService<HttpTenantContext>());

builder.Services.AddScoped<IOperadorActivo>(
    sp => sp.GetRequiredService<HttpTenantContext>());
// Caché distribuida con Redis
builder.Services.AddScoped<ICacheService, RedisCacheService>();

var app = builder.Build();

// Un solo proceso prepara la base. El servicio `inicializador` del compose corre con esta
// variable en true y termina; las réplicas de la API no inicializan nada, porque dos
// sembrando a la vez contra una base vacía violan el índice único. Acá entra MigrateAsync (15/10).
if (builder.Configuration.GetValue<bool>("Inicializacion:SoloInicializar"))
{
    await app.Services.InicializarBaseDeDatosAsync();
    return;
}

app.UseSeguridadWeb();
app.UseExceptionHandler();
app.UseLimiteDeTasa();

app.UseAuthentication();
app.UseLimitacionDeTasa();
app.UseAuthorization();
app.UseAntiforgery();

app.MapOpenApi();  // contrato en /openapi/v1.json (Scalar se agrega con la API pública)

// Salud (RNF 6.13): /live = el proceso responde; /ready = además llega a la base; /health = todo.
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false });
app.MapAutenticacionEndpoints();
app.MapVinculacionesComercialesEndpoints();
app.MapPersonalEndpoints();
app.MapBanderasEndpoints();


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

// GET /api/seguimiento/{token}
// Consulta pública de seguimiento para destinatarios.
// RF 41: el mismo enlace sirve para envíos y retiros; si el token no es de un envío, se prueba como retiro.
api.MapGet("/seguimiento/{token}", async (
    string token,
    ConsultarSeguimientoPublicoHandler handler,
    IDataProtectionProvider dataProtection,
    IConsultaPublicaRetiroRepository retiros,
    CancellationToken ct) =>
{
    var seguimiento = await handler.Handle(token, ct);
    if (seguimiento is not null)
        return Results.Ok(seguimiento);

    if (TokenRetiro.Leer(dataProtection, token) is not var (operadorId, retiroId))
        return Results.NotFound();

    var retiro = await retiros.ObtenerAsync(operadorId, retiroId, ct);

    return retiro is null
        ? Results.NotFound()
        : Results.Ok(new SeguimientoPublicoDto(
            "Retiro a domicilio",
            retiro.Estado,
            retiro.SolicitadoEn,
            retiro.FechaSolicitada,
            null, null, null, null,
            [],
            Tipo: "Retiro"));
})
.AllowAnonymous();

api.MapGet("/operadores", async (ListarOperadoresHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarOperadoresQuery(), ct)))
    .RequireAuthorization();

// Provisorio hasta el login del 15/10: el Portal elige con qué cuenta comercial operar.
api.MapGet("/cuentas-comerciales", async (
    ListarCuentasComercialesHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(
        new ListarCuentasComercialesQuery(), ct)))
    .RequireAuthorization(policy =>
    policy.RequireAuthenticatedUser()
          .RequireAssertion(context =>
              context.User.HasClaim("perfil_comercio", "AdministradorComercio") ||
              context.User.HasClaim("perfil_comercio", "Comercio") ||
              Perfiles.PersonalOperador.Any(context.User.IsInRole)));

// POST /api/envios → alta de envío desde el Portal del comercio.
api.MapPost("/envios", async (
    HttpContext context,
    IAntiforgery antiforgery,
    CrearEnvioCommand command,
    CrearEnvioHandler handler,
    CancellationToken ct) =>
{
    if (!ClaveApiAuthenticationHandler.EsLlamadaConClave(context) &&
        !await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Token de seguridad inválido",
            detail: "La solicitud no contiene un token antifalsificación válido.");
    }

    var resultado = await handler.Handle(command, ct);

    return Results.Created(
        $"/api/envios/{resultado.EnvioId}",
        new
        {
            id = resultado.EnvioId,
            tokenSeguimiento = resultado.TokenSeguimiento
        });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.OperadorYComercio));


 // GET /api/reportes/envios/resumen?desde=2026-11-01&hasta=2026-11-30
 // Resumen de envíos por período para el operador autenticado.
 api.MapGet("/reportes/envios/resumen", async (
     DateOnly desde,
     DateOnly hasta,
     HttpContext context,
     ObtenerResumenEnviosHandler handler,
     CancellationToken ct) =>
 {
     if (!Guid.TryParse(
             context.User.FindFirst("operador_id")?.Value,
             out var operadorId) ||
         operadorId == Guid.Empty)
     {
         return Results.Forbid();
     }

     if (hasta < desde || hasta == DateOnly.MaxValue)
     {
         return Results.BadRequest(new
         {
             mensaje = "El rango de fechas no es válido."
         });
     }

     var resultado = await handler.Handle(
         operadorId,
         desde,
         hasta,
         ct);

     return Results.Ok(resultado);
 })
 .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapGet("/envios/{envioId:guid}",
    async (
        Guid envioId,
        ObtenerDetalleEnvioHandler handler,
        CancellationToken ct) =>
    {
        var resultado = await handler.Handle(
            new ObtenerDetalleEnvioQuery(envioId),
            ct);

        return Results.Ok(resultado);
    })
    .RequireAuthorization();


api.MapGet("/reportes/motivos-no-entrega", async (
    DateOnly desde,
    DateOnly hasta,
    HttpContext context,
    ObtenerMotivosNoEntregaHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorId) ||
        operadorId == Guid.Empty)
    {
        return Results.Forbid();
    }

    if (hasta < desde || hasta == DateOnly.MaxValue)
    {
        return Results.BadRequest(new
        {
            mensaje = "El rango de fechas no es válido."
        });
    }

    var resultado = await handler.Handle(
        operadorId,
        desde,
        hasta,
        ct);

    return Results.Ok(resultado);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapGet("/reportes/actividad-comercial", async (
    DateOnly desde,
    DateOnly hasta,
    HttpContext context,
    ObtenerActividadComercialHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorId) ||
        operadorId == Guid.Empty)
    {
        return Results.Forbid();
    }

    if (hasta < desde || hasta == DateOnly.MaxValue)
    {
        return Results.BadRequest(new
        {
            mensaje = "El rango de fechas no es válido."
        });
    }

    var resultado = await handler.Handle(
        operadorId,
        desde,
        hasta,
        ct);

    return Results.Ok(resultado);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapGet("/reportes/repartidores", async (
    DateOnly desde,
    DateOnly hasta,
    HttpContext context,
    ObtenerResultadosRepartidoresHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorId) ||
        operadorId == Guid.Empty)
    {
        return Results.Forbid();
    }

    if (hasta < desde || hasta == DateOnly.MaxValue)
        return Results.BadRequest("Rango de fechas no válido.");

    return Results.Ok(await handler.Handle(
        operadorId, desde, hasta, ct));
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapGet("/reportes/cumplimiento", async (
    DateOnly desde,
    DateOnly hasta,
    HttpContext context,
    ObtenerCumplimientoHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorId) ||
        operadorId == Guid.Empty)
    {
        return Results.Forbid();
    }

    if (hasta < desde || hasta == DateOnly.MaxValue)
        return Results.BadRequest("Rango de fechas no válido.");

    var resultado = await handler.Handle(
        operadorId, desde, hasta, ct);

    return Results.Ok(resultado);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapGet("/reportes/liquidacion", async (
    DateOnly desde,
    DateOnly hasta,
    HttpContext context,
    ObtenerDetalleLiquidacionHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorId) ||
        operadorId == Guid.Empty)
    {
        return Results.Forbid();
    }

    if (hasta < desde || hasta == DateOnly.MaxValue)
        return Results.BadRequest("Rango de fechas no válido.");

    return Results.Ok(await handler.Handle(
        operadorId, desde, hasta, ct));
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapGet("/reportes/puntualidad", async (
    DateOnly desde,
    DateOnly hasta,
    HttpContext context,
    ObtenerPuntualidadHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorId) ||
        operadorId == Guid.Empty)
    {
        return Results.Forbid();
    }

    if (hasta < desde || hasta == DateOnly.MaxValue)
        return Results.BadRequest("Rango de fechas no válido.");

    return Results.Ok(await handler.Handle(
        operadorId, desde, hasta, ct));
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));



api.MapPost("/envios/importar-csv", async (
    HttpContext context,
    IAntiforgery antiforgery,
    IFormFile archivo,
    [FromForm] Guid cuentaComercialId,
    ImportarEnviosCsvHandler handler,
    CancellationToken ct) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Token de seguridad inválido",
            detail: "La solicitud no contiene un token antifalsificación válido.");
    }

    const long maxBytes = 5 * 1024 * 1024;

    if (archivo.Length == 0 || archivo.Length > maxBytes)
    {
        return Results.BadRequest(
            "El archivo debe tener contenido y no superar los 5 MB.");
    }

    if (!Path.GetExtension(archivo.FileName)
        .Equals(".csv", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(
            "El archivo debe tener extensión .csv.");
    }

    await using var stream = archivo.OpenReadStream();
    using var lector = new StreamReader(stream);

    var resultado = await handler.Handle(
        cuentaComercialId,
        lector,
        ct);

    return Results.Ok(resultado);
})
.DisableAntiforgery()
.RequireAuthorization(policy => policy.RequireRole(Perfiles.DespachoYComercio));

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
.RequireAuthorization(policy => policy.RequireRole(Perfiles.OperadorYComercio));

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
                request.EvidenciaFallo,
                request.BultosEntregados,
                request.BultosNoEntregados?
                    .Select(b => new BultoNoEntregadoMovil(
                        b.BultoId,
                        b.Motivo,
                        b.Evidencia))
                    .ToArray()), ct);
        return Results.Created($"/api/envios/{envioId}/eventos-movil/{request.IdEvento}", new { id = request.IdEvento });
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Repartidor));

api.MapPost("/envios/{envioId:guid}/recepcion",
    async (
        Guid envioId,
        RecibirEnDepositoRequest request,
        RecibirEnDepositoHandler handler,
        CancellationToken ct) =>
    {
        var resultado = await handler.Handle(
            new RecibirEnDepositoCommand(
                envioId,
                request.IdentificacionesEscaneadas,
                request.Responsable),
            ct);

        // RF 10: 200 con el detalle; "recibido: false" indica que faltan bultos declarados.
        return Results.Ok(resultado);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Deposito));

api.MapGet("/envios/{envioId:guid}/discrepancias-recepcion",
    async (
        Guid envioId,
        ListarDiscrepanciasRecepcionHandler handler,
        CancellationToken ct) =>
        Results.Ok(await handler.Handle(envioId, ct)))
.RequireAuthorization(policy => policy.RequireRole(
    Perfiles.AdministradorOperador,
    Perfiles.Operador));

api.MapPost("/envios/{envioId:guid}/retorno-deposito",
    async (
        Guid envioId,
        RecibirEnDepositoRequest request,
        RetornarADepositoHandler handler,
        CancellationToken ct) =>
    {
        await handler.Handle(
            new RetornarADepositoCommand(
                envioId,
                request.IdentificacionesEscaneadas,
                request.Responsable),
            ct);

        return Results.NoContent();
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Deposito));


api.MapPost("/envios/{envioId:guid}/iniciar-devolucion",
    async (
        Guid envioId,
        IniciarDevolucionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        IniciarDevolucionHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        if (tenant.OperadorId is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operador no identificado");
        }

        await handler.Handle(
            new IniciarDevolucionCommand(
                envioId,
                request.Responsable),
            ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapPost("/envios/{envioId:guid}/confirmar-devolucion",
    async (
        Guid envioId,
        ConfirmarDevolucionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ConfirmarDevolucionHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        await handler.Handle(
            new ConfirmarDevolucionCommand(
                request.CuentaComercialId,
                envioId,
                request.IdentificacionesEscaneadas,
                request.Responsable,
                request.Modalidad,
                request.Receptor,
                request.ReferenciaConstancia),
            ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.PersonalComercio));

// RF37: solicitud de retiro en domicilio desde el Portal del comercio.
api.MapPost("/retiros", async (
    HttpContext context,
    IAntiforgery antiforgery,
    CrearRetiroCommand command,
    CrearRetiroHandler handler,
    CancellationToken ct) =>
{
    if (!ClaveApiAuthenticationHandler.EsLlamadaConClave(context) &&
        !await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Token de seguridad inválido",
            detail: "La solicitud no contiene un token antifalsificación válido.");
    }

    var id = await handler.Handle(command, ct);

    return Results.Created(
        $"/api/retiros/{id}",
        new { id });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.PersonalComercio));

// RF37: listar retiros del comercio o del operador autenticado.
api.MapGet("/retiros", async (
    ConsultarRetirosHandler handler,
    CancellationToken ct) =>
{
    var resultado = await handler.ListarAsync(ct);

    return Results.Ok(resultado);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.OperadorYComercio));


// RF37: consultar el detalle de un retiro.
api.MapGet("/retiros/{retiroId:guid}", async (
    Guid retiroId,
    ConsultarRetirosHandler handler,
    CancellationToken ct) =>
{
    var retiro = await handler.ObtenerAsync(
        retiroId,
        ct);

    return retiro is null
        ? Results.NotFound()
        : Results.Ok(retiro);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.OperadorYComercio));

// Declarar un envío como extraviado.
api.MapPost("/envios/{envioId:guid}/extravio",
    async (
        Guid envioId,
        DeclararExtravioRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        DeclararExtravioHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        if (tenant.OperadorId is not Guid operadorId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operador no identificado");
        }

        await handler.Handle(
            new DeclararExtravioCommand(
                operadorId,
                envioId,
                request.Justificacion,
                request.Responsable),
            ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapPost("/retiros/{retiroId:guid}/recibir-deposito",
    async (
        Guid retiroId,
        HttpContext context,
        IAntiforgery antiforgery,
        GestionarRetiroHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        await handler.RecibirEnDepositoAsync(retiroId, ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Deposito));


// POST /api/retiros/{id}/reprogramar → un retiro no retirado vuelve a Solicitado (RF 38).
api.MapPost("/retiros/{retiroId:guid}/reprogramar",
    async (
        Guid retiroId,
        HttpContext context,
        IAntiforgery antiforgery,
        GestionarRetiroHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        await handler.ReprogramarAsync(retiroId, ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapPost("/retiros/{retiroId:guid}/entregar-comercio",
    async (
        Guid retiroId,
        HttpContext context,
        IAntiforgery antiforgery,
        GestionarRetiroHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        await handler.EntregarAlComercioAsync(retiroId, ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Deposito));

api.MapPost("/retiros/{retiroId:guid}/eventos-movil",
    async (
        Guid retiroId,
        RegistrarEventoRetiroCommand request,
        RegistrarEventoRetiroHandler handler,
        CancellationToken ct) =>
    {
        if (retiroId != request.RetiroId)
            return Results.BadRequest(
                "El identificador del retiro no coincide.");

        await handler.Handle(request, ct);

        return Results.Created(
            $"/api/retiros/{retiroId}/eventos-movil/{request.IdEvento}",
            new { id = request.IdEvento });
    })
    .RequireAuthorization(policy =>
        policy.RequireRole(Perfiles.Repartidor));

api.MapGet("/reportes/retiros/resumen",
    async (
        DateOnly desde,
        DateOnly hasta,
        ObtenerResumenRetirosHandler handler,
        CancellationToken ct) =>
    {
        var resultado = await handler.Handle(
            desde, hasta, ct);

        return Results.Ok(resultado);
    })
    .RequireAuthorization(policy =>
        policy.RequireRole(Perfiles.Despacho));

// Generar enlace para un retiro que el usuario puede consultar.
api.MapGet("/retiros/{retiroId:guid}/enlace-publico",
    async (
        Guid retiroId,
        ConsultarRetirosHandler consultas,
        IDataProtectionProvider dataProtection,
        CancellationToken ct) =>
    {
        var retiro = await consultas.ObtenerAsync(retiroId, ct);

        if (retiro is null)
            return Results.NotFound();

        var token = TokenRetiro.Proteger(dataProtection, retiro.OperadorId, retiro.Id);

        // RF 41: el mismo enlace público que los envíos (página /seguimiento del Portal).
        return Results.Ok(new
        {
            Url = $"/seguimiento/{token}",
            VigenciaDias = TokenRetiro.VigenciaDias
        });
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.OperadorYComercio));



// Registrar la reaparición de un envío extraviado.
api.MapPost("/envios/{envioId:guid}/reaparicion",
    async (
        Guid envioId,
        RegistrarReaparicionRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        RegistrarReaparicionHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        if (tenant.OperadorId is not Guid operadorId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operador no identificado");
        }

        await handler.Handle(
            new RegistrarReaparicionCommand(
                operadorId,
                envioId,
                request.IdentificacionesEscaneadas,
                request.Responsable),
            ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Deposito));

// Solicitudes de reprogramación pendientes para Backoffice.

// Solicitar la reprogramación de un envío desde Backoffice.
api.MapPost("/solicitudes-reprogramacion",
    async (
        HttpContext context,
        IAntiforgery antiforgery,
        SolicitarReprogramacionCommand command,
        SolicitarReprogramacionHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido",
                detail: "La solicitud no contiene un token antifalsificación válido.");
        }

        var solicitudId = await handler.Handle(command, ct);

        return Results.Ok(new
        {
            SolicitudId = solicitudId,
            Mensaje = "Solicitud de reprogramación creada correctamente."
        });
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapGet("/solicitudes-reprogramacion/pendientes",
    async (
        ListarSolicitudesReprogramacionHandler handler,
        CancellationToken ct) =>
    {
        var solicitudes = await handler.Handle(ct);
        return Results.Ok(solicitudes);
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapPost("/solicitudes-reprogramacion/{solicitudId:guid}/confirmar",
    async (
        Guid solicitudId,
        HttpContext context,
        IAntiforgery antiforgery,
        ConfirmarReprogramacionRequest request,
        ConfirmarReprogramacionHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido",
                detail: "La solicitud no contiene un token antifalsificación válido.");
        }

        await handler.Handle(
            new ConfirmarReprogramacionCommand(
                solicitudId,
                request.Responsable),
            ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

// Actualizar una tarifa existente: crea una nueva versión.
api.MapPost("/tarifas", async (
    ActualizarTarifaCommand command,
    HttpContext context,
    IAntiforgery antiforgery,
    ActualizarTarifaHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorSesion) ||
        operadorSesion != command.OperadorId)
    {
        return Results.Forbid();
    }

    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Token de seguridad inválido");
    }

    var id = await handler.Handle(command, ct);

    return Results.Ok(new { id });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));


// Crear un cuadro tarifario y su primera versión.
api.MapPost("/tarifas/nueva", async (
    CrearTarifaCommand command,
    HttpContext context,
    IAntiforgery antiforgery,
    CrearTarifaHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorSesion) ||
        operadorSesion != command.OperadorId)
    {
        return Results.Forbid();
    }

    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Token de seguridad inválido");
    }

    var id = await handler.Handle(command, ct);

    return Results.Created(
        $"/api/tarifas/vigente",
        new { id });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));


// Consultar la tarifa vigente de un servicio.
api.MapGet("/tarifas/vigente", async (
    Guid operadorId,
    Guid zonaCoberturaId,
    string tipoServicio,
    HttpContext context,
    ObtenerTarifaVigenteHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorSesion) ||
        operadorSesion != operadorId)
    {
        return Results.Forbid();
    }

    var tarifa = await handler.Handle(
        new ObtenerTarifaVigenteQuery(
            operadorSesion,
            zonaCoberturaId,
            tipoServicio),
        ct);

    return tarifa is null
        ? Results.NotFound()
        : Results.Ok(tarifa);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapGet("/reglas-operativas/vigente", async (
    Guid operadorId,
    HttpContext context,
    ObtenerReglasOperativasVigentesHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorSesion) ||
        operadorSesion != operadorId)
    {
        return Results.Forbid();
    }

    var reglas = await handler.Handle(
        new ObtenerReglasOperativasVigentesQuery(operadorSesion),
        ct);

    return reglas is null
        ? Results.NotFound()
        : Results.Ok(reglas);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapPost("/reglas-operativas", async (
    ActualizarReglasOperativasCommand command,
    HttpContext context,
    ActualizarReglasOperativasHandler handler,
    CancellationToken ct) =>
{
    if (!Guid.TryParse(
            context.User.FindFirst("operador_id")?.Value,
            out var operadorSesion) ||
        operadorSesion != command.OperadorId)
    {
        return Results.Forbid();
    }

    var id = await handler.Handle(command, ct);

    return Results.Ok(new { id });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));

// GET /api/motivos-no-entrega → catálogo vigente del operador para la app del repartidor (RF 32).
// La app lo guarda en el teléfono para elegir el motivo sin conexión.
api.MapGet("/motivos-no-entrega", async (
    ITenantContext tenant,
    ObtenerReglasOperativasVigentesHandler handler,
    IBanderas banderas,
    CancellationToken ct) =>
{
    var operadorId = tenant.OperadorId ?? throw new UnauthorizedAccessException("Primero hay que elegir un operador.");
    var reglas = await handler.Handle(new ObtenerReglasOperativasVigentesQuery(operadorId), ct);

    return Results.Ok(new
    {
        // Regla del operador y bandera de la plataforma (aclaración 3.2): las dos tienen que permitirla.
        admiteEntregaParcial = (reglas?.AdmiteEntregaParcial ?? false) &&
            await banderas.EstaHabilitadaAsync(Banderas.EntregaParcial, operadorId, ct),
        motivos = reglas?.Motivos.Select(m => m.Descripcion).ToArray() ?? []
    });
})
.RequireAuthorization(policy => policy.RequireRole([Perfiles.Repartidor, .. Perfiles.PersonalOperador]));

api.MapGet("/zonas", async (Guid operadorId, ListarZonasHandler handler, CancellationToken ct) =>
{
    var zonas = await handler.Handle(
        new ListarZonasQuery(operadorId),
        ct);

    return Results.Ok(zonas);
})
.RequireAuthorization();


api.MapPost("/zonas", async (
    CrearZonaCommand command,
    CrearZonaHandler handler,
    CancellationToken ct) =>
{
    var id = await handler.Handle(command, ct);

    return Results.Ok(new { id });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));

// GET /api/zonas/{id}?operadorId=... → detalle de una zona, para el formulario de edición del Backoffice.
api.MapGet("/zonas/{zonaId:guid}", async (
    Guid zonaId,
    Guid operadorId,
    ObtenerZonaHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ObtenerZonaQuery(operadorId, zonaId), ct)))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));

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
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));

api.MapPost("/hojas-de-ruta", async (
    CrearHojaDeRutaRequest request,
    HttpContext context,
    IAntiforgery antiforgery,
    ITenantContext tenant,
    CrearHojaDeRutaHandler handler,
    CancellationToken ct) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Token de seguridad inválido");
    }

    if (tenant.OperadorId is not Guid operadorId)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Operador no identificado");
    }

    var id = await handler.Handle(
        new CrearHojaDeRutaCommand(
            operadorId,
            request.RepartidorId,
            request.VehiculoId,
            request.Fecha,
            request.HoraSalida,
            request.HoraEntregaDesde,
            request.HoraEntregaHasta,
            request.MaximoParadas,
            request.EnviosIds ?? [],
            request.RetirosIds ?? [],
            request.DevolucionesIds ?? [],
            request.EntregasRetirosIds ?? []),
        ct);

    return Results.Created(
        $"/api/hojas-de-ruta/{id}",
        new { id });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapGet("/hojas-de-ruta/opciones", async (
    DateOnly? fecha,
    TimeOnly? horaDesde,
    TimeOnly? horaHasta,
    ITenantContext tenant,
    ConsultarHojasDeRutaHandler handler,
    CancellationToken ct) =>
{
    if (tenant.OperadorId is not Guid operadorId)
        return Results.Forbid();

    var opciones = await handler.ObtenerOpcionesAsync(
        operadorId,
        fecha,
        horaDesde,
        horaHasta,
        ct);

    return Results.Ok(opciones);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapGet("/hojas-de-ruta", async (
    ITenantContext tenant,
    ConsultarHojasDeRutaHandler handler,
    CancellationToken ct) =>
{
    if (tenant.OperadorId is not Guid operadorId)
        return Results.Forbid();

    var hojas = await handler.ListarAsync(operadorId, ct);

    return Results.Ok(hojas);
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));


api.MapPost("/hojas-de-ruta/{hojaId:guid}/despachar",
    async (
        Guid hojaId,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        DespacharHojaDeRutaHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        if (tenant.OperadorId is not Guid operadorId)
            return Results.Forbid();

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
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

// RF 22: rendición al regresar al depósito. GET muestra lo que tiene que volver; POST lo recibe y cierra la hoja.
api.MapGet("/hojas-de-ruta/{hojaId:guid}/rendicion",
    async (Guid hojaId, RendirHojaDeRutaHandler handler, CancellationToken ct) =>
        Results.Ok(await handler.ObtenerAsync(hojaId, ct)))
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorOperador, Perfiles.Operador));

api.MapPost("/hojas-de-ruta/{hojaId:guid}/rendicion",
    async (
        Guid hojaId,
        RendirHojaDeRutaRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        RendirHojaDeRutaHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        var resultado = await handler.Handle(
            new RendirHojaDeRutaCommand(hojaId, request.BultosEscaneados ?? [], request.Responsable ?? string.Empty),
            ct);

        return Results.Ok(resultado);
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorOperador, Perfiles.Operador));

api.MapDelete("/hojas-de-ruta/{hojaId:guid}/envios/{envioId:guid}",
    async (
        Guid hojaId,
        Guid envioId,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        [FromBody] QuitarEnvioDeRutaRequest request,
        QuitarEnvioDeRutaHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido",
                detail: "La solicitud no contiene un token antifalsificación válido.");
        }

        if (tenant.OperadorId is not Guid operadorId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operador no identificado",
                detail: "El usuario autenticado no tiene un operador asociado.");
        }

        await handler.Handle(
            new QuitarEnvioDeRutaCommand(
                operadorId,
                hojaId,
                envioId,
                request.Responsable),
            ct);

        return Results.NoContent();
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapPost("/hojas-de-ruta/{hojaId:guid}/envios",
    async (
        Guid hojaId,
        AgregarEnvioARutaRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        AgregarEnvioARutaHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        if (tenant.OperadorId is not Guid operadorId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operador no identificado");
        }

        var horarios = await handler.Handle(
            new AgregarEnvioARutaCommand(
                operadorId,
                hojaId,
                request.EnvioId,
                request.HoraEntregaDesde,
                request.HoraEntregaHasta),
            ct);

        return Results.Ok(new
        {
            hojaDeRutaId = hojaId,
            horarios
        });
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

api.MapPost("/hojas-de-ruta/{hojaId:guid}/retiros",
    async (
        Guid hojaId,
        AgregarRetiroARutaRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        AgregarRetiroARutaHandler handler,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido");
        }

        if (tenant.OperadorId is not Guid operadorId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operador no identificado");
        }

        var horarios = await handler.Handle(
            new AgregarRetiroARutaCommand(
                operadorId,
                hojaId,
                request.RetiroId),
            ct);

        return Results.Ok(new
        {
            hojaDeRutaId = hojaId,
            horarios
        });
    })
    .RequireAuthorization(policy => policy.RequireRole(Perfiles.Despacho));

// GET /api/repartidor/hojas-de-ruta?fecha=2026-10-28 → las hojas de ruta del repartidor logueado en la app,
// con sus paradas y los bultos a cargar. La fecha la manda el teléfono (es "hoy" en Uruguay, no en UTC).
api.MapGet("/repartidor/hojas-de-ruta", async (
    DateOnly fecha,
    ObtenerMisHojasDeRutaHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ObtenerMisHojasDeRutaQuery(fecha), ct)))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Repartidor));

// POST /api/repartidor/hojas-de-ruta/{id}/despachar → el repartidor terminó de cargar el vehículo (RF 17 y 19).
// Manda los bultos que escaneó; si falta o sobra alguno responde 400 con la lista y no despacha nada.
api.MapPost("/repartidor/hojas-de-ruta/{hojaId:guid}/despachar", async (
    Guid hojaId,
    DespacharMiHojaDeRutaRequest request,
    DespacharMiHojaDeRutaHandler handler,
    CancellationToken ct) =>
{
    var cantidad = await handler.Handle(
        new DespacharMiHojaDeRutaCommand(hojaId, request.BultosEscaneados),
        ct);

    return Results.Ok(new { hojaDeRutaId = hojaId, enviosEnTransito = cantidad });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Repartidor));

// POST /api/repartidor/posiciones → lote de posiciones que la app tomó durante la jornada (RF 23),
// muchas veces con la pantalla apagada o sin señal. Idempotente: reenviar un lote no duplica nada.
api.MapPost("/repartidor/posiciones", async (
    RegistrarPosicionesCommand command,
    RegistrarPosicionesHandler handler,
    CancellationToken ct) =>
    Results.Ok(new { guardadas = await handler.Handle(command, ct) }))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.Repartidor));

// POST /api/dispositivos → la app registra el token de Firebase del teléfono al iniciar sesión.
// El usuario y el operador salen de la cookie; si el token ya estaba, se renueva (no se duplica).
api.MapPost("/dispositivos", async (
    RegistrarDispositivoCommand command,
    RegistrarDispositivoHandler handler,
    CancellationToken ct) =>
{
    await handler.Handle(command, ct);
    return Results.NoContent();
})
.RequireAuthorization();

// POST /api/notificaciones/prueba → aviso a los teléfonos del usuario logueado (probar push con la app cerrada).
api.MapPost("/notificaciones/prueba", async (
    EnviarNotificacionDePruebaCommand command,
    EnviarNotificacionDePruebaHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(command, ct)))
.RequireAuthorization();

// GET /api/avisos?estado=Fallida → historial de avisos a comercios (RNF 6.9). El comercio ve solo
// los de su cuenta; el operador, todos los suyos (consulta de fallidos, RNF 6.8).
api.MapGet("/avisos", async (
    EstadoEntregaAviso? estado,
    ListarAvisosHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarAvisosQuery(estado), ct)))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.DespachoYComercio));

// POST /api/avisos/{id}/reenviar → vuelve a encolar un aviso fallido; lo entrega el Worker.
api.MapPost("/avisos/{entregaId:guid}/reenviar", async (
    Guid entregaId,
    ReenviarAvisoHandler handler,
    CancellationToken ct) =>
{
    await handler.Handle(new ReenviarAvisoCommand(entregaId), ct);
    return Results.NoContent();
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.DespachoYComercio));

// GET /api/suscripciones-aviso → a qué URL y tipos de evento avisa la plataforma, con el secreto de la firma.
api.MapGet("/suscripciones-aviso", async (
    ListarSuscripcionesHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarSuscripcionesQuery(), ct)))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorOperador, Perfiles.AdministradorComercio, Perfiles.Comercio));

// POST /api/suscripciones-aviso → alta de una suscripción; el secreto lo genera la plataforma.
api.MapPost("/suscripciones-aviso", async (
    CrearSuscripcionCommand command,
    CrearSuscripcionHandler handler,
    CancellationToken ct) =>
{
    var id = await handler.Handle(command, ct);
    return Results.Created($"/api/suscripciones-aviso/{id}", new { id });
})
.RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorOperador, Perfiles.AdministradorComercio, Perfiles.Comercio));

// GET /api/mensajes-fallidos → colas *_error del broker con una muestra de sus mensajes (RNF 6.8).
// Solo el administrador de plataforma: las colas mezclan mensajes de todos los operadores.
api.MapGet("/mensajes-fallidos", async (
    ListarMensajesFallidosHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarMensajesFallidosQuery(), ct)))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorPlataforma));

// POST /api/mensajes-fallidos/{cola}/reintentar → devuelve los mensajes a su cola original; los procesa el Worker.
api.MapPost("/mensajes-fallidos/{cola}/reintentar", async (
    string cola,
    ReintentarMensajesFallidosHandler handler,
    CancellationToken ct) =>
    Results.Ok(new { reintentados = await handler.Handle(new ReintentarMensajesFallidosCommand(cola), ct) }))
.RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorPlataforma));

app.Run();

// Las sondas de salud no se instrumentan: al descartarse la span del request, la consulta
// de Npgsql queda sin padre grabado y el muestreo la descarta también.
static bool EsSonda(PathString ruta) =>
    ruta.StartsWithSegments("/health") || ruta.StartsWithSegments("/ready") || ruta.StartsWithSegments("/live");

// Permite usar Program en las pruebas de integración más adelante.
public partial class Program { }

/// <summary>Cuerpo que manda la app del repartidor; el envío viaja en la URL.</summary>

public sealed record BultoNoEntregadoRequest( Guid BultoId, string Motivo, string Evidencia);

public sealed record EventoMovilRequest( Guid IdEvento, string Tipo, string? Motivo, string? Receptor, DateTimeOffset FechaHora, string? DocumentoReceptor = null, string? ReferenciaFirma = null, string? ReferenciaFotografia = null, decimal? Latitud = null, decimal? Longitud = null, string? EvidenciaFallo = null, IReadOnlyCollection<Guid>? BultosEntregados = null,
    IReadOnlyCollection<BultoNoEntregadoRequest>? BultosNoEntregados = null);public sealed record RecibirEnDepositoRequest( IReadOnlyList<string> IdentificacionesEscaneadas, string Responsable);
public sealed record ConfirmarReprogramacionRequest( string Responsable);
public sealed record QuitarEnvioDeRutaRequest(string Responsable);
public sealed record AgregarEnvioARutaRequest( Guid EnvioId, TimeOnly HoraEntregaDesde, TimeOnly HoraEntregaHasta);

/// <summary>Cuerpo de PUT /api/zonas/{id}; la zona viaja en la URL.</summary>
public sealed record EditarZonaRequest(
    Guid OperadorId,
    string Nombre,
    string Departamento,
    int MinutosTraslado,
    IReadOnlyList<CoberturaCommand> Coberturas,
    IReadOnlyList<FranjaEntregaCommand> Franjas);

/// <summary>Cuerpo de POST /api/repartidor/hojas-de-ruta/{id}/despachar; la hoja viaja en la URL.</summary>
public sealed record DespacharMiHojaDeRutaRequest(IReadOnlyList<string> BultosEscaneados);

public sealed record DeclararExtravioRequest( string Justificacion, string Responsable);
public sealed record RegistrarReaparicionRequest(  IReadOnlyList<string> IdentificacionesEscaneadas, string Responsable);
public sealed record IniciarDevolucionRequest( string Responsable);

public sealed record ConfirmarDevolucionRequest( Guid CuentaComercialId, IReadOnlyList<string> IdentificacionesEscaneadas, string Responsable, string Modalidad, string Receptor, string ReferenciaConstancia);

public sealed record AgregarRetiroARutaRequest(Guid RetiroId);
public sealed record CrearHojaDeRutaRequest( Guid RepartidorId, Guid VehiculoId, DateOnly Fecha, TimeOnly HoraSalida, TimeOnly HoraEntregaDesde, TimeOnly HoraEntregaHasta,  int MaximoParadas, IReadOnlyList<Guid>? EnviosIds, IReadOnlyList<Guid>? RetirosIds = null, IReadOnlyList<Guid>? DevolucionesIds = null, IReadOnlyList<Guid>? EntregasRetirosIds = null);

/// <summary>RF 22: lo escaneado al volver al depósito y quién lo recibe.</summary>
public sealed record RendirHojaDeRutaRequest(IReadOnlyList<string>? BultosEscaneados, string? Responsable);
