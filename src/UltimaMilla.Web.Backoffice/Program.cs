using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using UltimaMilla.Web.Backoffice.Services;

var builder = WebApplication.CreateBuilder(args);

// Redis compartido con API y Portal.
var redisConnectionString =
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:Redis.");

var redis = await ConnectionMultiplexer.ConnectAsync(
    redisConnectionString);

builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

// Compartir las claves de Data Protection.
builder.Services
    .AddDataProtection()
    .SetApplicationName("UltimaMilla")
    .PersistKeysToStackExchangeRedis(
        redis,
        "UltimaMilla:DataProtection:Keys");

// Reconocer la cookie de Identity emitida por la API.
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "UltimaMilla.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccesoDenegado";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OperadorBackoffice", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(
                  "AdministradorOperador",
                  "Operador"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder(
        "/",
        "OperadorBackoffice");

    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/SeleccionarOperador");
});

// Clientes HTTP existentes.
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<CookieAutenticacionHandler>();

builder.Services.AddHttpClient<EnviosApiClient>(c =>
    c.BaseAddress = new Uri(
        builder.Configuration["Api:BaseUrl"]
        ?? "http://localhost:5080/"))
    .AddHttpMessageHandler<CookieAutenticacionHandler>();

builder.Services.AddHttpClient<TarifasApiClient>(c =>
    c.BaseAddress = new Uri(
        builder.Configuration["Api:BaseUrl"]
        ?? "http://localhost:5080/"))
    .AddHttpMessageHandler<CookieAutenticacionHandler>();

builder.Services.AddHttpClient<ReglasOperativasApiClient>(c =>
    c.BaseAddress = new Uri(
        builder.Configuration["Api:BaseUrl"]
        ?? "http://localhost:5080/"))
    .AddHttpMessageHandler<CookieAutenticacionHandler>();

builder.Services.AddHttpClient<ZonasApiClient>(c =>
    c.BaseAddress = new Uri(
        builder.Configuration["Api:BaseUrl"]
        ?? "http://localhost:5080/"))
    .AddHttpMessageHandler<CookieAutenticacionHandler>();

builder.Services.AddHttpClient<HojasDeRutaApiClient>(c =>
    c.BaseAddress = new Uri(
        builder.Configuration["Api:BaseUrl"]
        ?? "http://localhost:5080/"))
    .AddHttpMessageHandler<CookieAutenticacionHandler>();

// Observabilidad.
builder.Logging.AddOpenTelemetry(
    o => o.IncludeFormattedMessage = true);

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

app.MapRazorPages();

app.Run();