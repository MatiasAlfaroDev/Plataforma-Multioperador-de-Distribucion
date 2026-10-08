using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UltimaMilla.Api.Errores;
using UltimaMilla.Api.Salud;
using UltimaMilla.Application;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarCuentasComerciales;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Application.Envios.Queries.ListarEnvios;
using UltimaMilla.Application.Operadores.Queries.ListarOperadores;
using UltimaMilla.Infrastructure;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UltimaMilla.Api.Auth;

var builder = WebApplication.CreateBuilder(args);
// Servicios necesarios para ASP.NET Core Identity.
// ---------------------------------------------------------
// AUTENTICACIÓN JWT
// ---------------------------------------------------------

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Falta configurar Jwt:Key.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Falta configurar Jwt:Issuer.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Falta configurar Jwt:Audience.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "La clave JWT debe tener al menos 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                RoleClaimType = System.Security.Claims.ClaimTypes.Role,

                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization();
builder.Services.AddDataProtection();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();   // contrato en /openapi/v1.json (Scalar se agrega con la API pública)

// Salud (RNF 6.13): /live = el proceso responde; /ready = además llega a la base; /health = todo.
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false });

app.MapAuthEndpoints();
var api = app.MapGroup("/api");

api.MapGet("/operadores", async (ListarOperadoresHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarOperadoresQuery(), ct)))
    .RequireAuthorization();;

// Provisorio hasta el login del 15/10: el Portal elige con qué cuenta comercial operar.
api.MapGet("/cuentas-comerciales", async (ListarCuentasComercialesHandler handler, CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarCuentasComercialesQuery(), ct)))
    .RequireAuthorization();

// POST /api/envios → alta de envío desde el Portal del comercio.
// POST /api/envios → alta de envío desde el Portal del comercio.
api.MapPost("/envios", async (
    CrearEnvioCommand command,
    CrearEnvioHandler handler,
    CancellationToken ct) =>
{
    var id = await handler.Handle(command, ct);

    return Results.Created(
        $"/api/envios/{id}",
        new { id });
})
.RequireAuthorization();

// GET /api/envios → listado de envíos del operador autenticado.
api.MapGet("/envios", async (
    ListarEnviosHandler handler,
    CancellationToken ct) =>
    Results.Ok(await handler.Handle(new ListarEnviosQuery(), ct)))
    .RequireAuthorization();
app.Run();

// Permite usar Program en las pruebas de integración más adelante.
public partial class Program { }
