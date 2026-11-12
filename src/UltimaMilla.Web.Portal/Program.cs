using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using UltimaMilla.Web.Portal.Components;
using UltimaMilla.Web.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);// Compartir las claves de autenticación con la API.
var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:Redis.");

var redis = await ConnectionMultiplexer.ConnectAsync(
    redisConnectionString);

builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services
    .AddDataProtection()
    .SetApplicationName("UltimaMilla")
    .PersistKeysToStackExchangeRedis(
        redis,
        "UltimaMilla:DataProtection:Keys");

// Reconocer las cookies emitidas por la API.
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "UltimaMilla.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.LoginPath = "/login";
        options.AccessDeniedPath = "/acceso-denegado";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ComercioPortal", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(
                  "perfil_comercio",
                  "AdministradorComercio",
                  "Comercio"));
    options.AddPolicy("AdministradorComercio", policy =>
        policy.RequireClaim("perfil_comercio", "AdministradorComercio"));
});
builder.Services.AddCascadingAuthenticationState();



// Blazor InteractiveServer (informe 6.1). Data Protection ya está en Redis; con más de una réplica
// Traefik mantiene cada circuito en su instancia con la cookie de afinidad del compose.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<EnviosApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/"));

builder.Services.AddHttpClient<SeguimientoApiClient>(c =>
    c.BaseAddress = new Uri(
        builder.Configuration["Api:BaseUrl"]
        ?? "http://localhost:5080/"));

// Telemetría (RNF 6.12): sale por OTLP al Collector, igual que la API. La instrumentación de
// HttpClient es la que propaga el traceparent, y por eso la traza se ve de punta a punta:
// Portal → API → PostgreSQL con un solo traceId.
builder.Logging.AddOpenTelemetry(o => o.IncludeFormattedMessage = true);
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .UseOtlpExporter();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


app.Run();
