
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Identidad;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

public static class AutenticacionEndpoints
{
    private const string MembresiaClaim = "membresia_id";
    private const string OperadorClaim = "operador_id";
    private const string PerfilClaim = "perfil";
    private const string CuentaComercialClaim = "cuenta_comercial_id";

    public static RouteGroupBuilder MapAutenticacionEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth")
            .WithTags("Autenticación");

        grupo.MapPost("/login", async (
            LoginRequest request,
            UserManager<Usuario> userManager,
            HttpContext httpContext) =>
        {
            var usuario = await userManager.FindByEmailAsync(request.Email);

            if (usuario is null || !usuario.Activo)
                return Results.Unauthorized();

            var passwordValida = await userManager.CheckPasswordAsync(
                usuario,
                request.Password);

            if (!passwordValida)
                return Results.Unauthorized();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(
                    ClaimTypes.Name,
                    usuario.UserName ?? usuario.Email ?? string.Empty)
            };

            await IniciarSesionAsync(httpContext, claims);

            return Results.Ok(new
            {
                usuario.Id,
                usuario.Email,
                usuario.NombreCompleto
            });
        });

        grupo.MapGet("/membresias", async (
            HttpContext context,
            AppDbContext db) =>
        {
            if (!ObtenerUsuarioId(context, out var usuarioId))
                return Results.Unauthorized();

            var membresias = await db.Set<MembresiaUsuario>()
                .AsNoTracking()
                .Where(m => m.UsuarioId == usuarioId && m.Activa)
                .Select(m => new
                {
                    m.Id,
                    m.OperadorId,
                    m.CuentaComercialId,
                    m.Perfil
                })
                .ToListAsync();

            return Results.Ok(membresias);
        }).RequireAuthorization();

        grupo.MapPost("/seleccionar-operador", async (
            SeleccionarOperadorRequest request,
            HttpContext context,
            AppDbContext db,
            UserManager<Usuario> userManager) =>
        {
            if (!ObtenerUsuarioId(context, out var usuarioId))
                return Results.Unauthorized();

            var usuario = await userManager.FindByIdAsync(
                usuarioId.ToString());

            if (usuario is null || !usuario.Activo)
                return Results.Unauthorized();

            var membresia = await db.Set<MembresiaUsuario>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.Id == request.MembresiaId &&
                    m.UsuarioId == usuarioId &&
                    m.Activa);

            if (membresia is null)
                return Results.Forbid();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(
                    ClaimTypes.Name,
                    usuario.UserName ?? usuario.Email ?? string.Empty),
                new(MembresiaClaim, membresia.Id.ToString()),
                new(OperadorClaim, membresia.OperadorId.ToString()),
                new(PerfilClaim, membresia.Perfil),
                new(ClaimTypes.Role, membresia.Perfil)
            };

            if (membresia.CuentaComercialId.HasValue)
            {
                claims.Add(new Claim(
                    CuentaComercialClaim,
                    membresia.CuentaComercialId.Value.ToString()));
            }

            await IniciarSesionAsync(context, claims);

            return Results.Ok(new
            {
                membresia.Id,
                membresia.OperadorId,
                membresia.CuentaComercialId,
                membresia.Perfil
            });
        }).RequireAuthorization();

        grupo.MapGet("/me", (HttpContext context) =>
        {
            return Results.Ok(new
            {
                Autenticado = context.User.Identity?.IsAuthenticated ?? false,
                UsuarioId = context.User.FindFirstValue(
                    ClaimTypes.NameIdentifier),
                MembresiaId = context.User.FindFirstValue(MembresiaClaim),
                OperadorId = context.User.FindFirstValue(OperadorClaim),
                CuentaComercialId = context.User.FindFirstValue(
                    CuentaComercialClaim),
                Perfil = context.User.FindFirstValue(PerfilClaim)
            });
        }).RequireAuthorization();

        grupo.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(
                IdentityConstants.ApplicationScheme);

            return Results.NoContent();
        }).RequireAuthorization();

        return grupo;
    }

    private static bool ObtenerUsuarioId(
        HttpContext context,
        out Guid usuarioId)
    {
        return Guid.TryParse(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out usuarioId);
    }

    private static Task IniciarSesionAsync(
        HttpContext context,
        IEnumerable<Claim> claims)
    {
        var identidad = new ClaimsIdentity(
            claims,
            IdentityConstants.ApplicationScheme);

        var principal = new ClaimsPrincipal(identidad);

        return context.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal);
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record SeleccionarOperadorRequest(Guid MembresiaId);
