using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using UltimaMilla.Infrastructure.Identidad;

namespace UltimaMilla.Api.Endpoints;

public static class AutenticacionEndpoints
{
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

            var claims = new List<System.Security.Claims.Claim>
            {
                new(
                    System.Security.Claims.ClaimTypes.NameIdentifier,
                    usuario.Id.ToString()),

                new(
                    System.Security.Claims.ClaimTypes.Name,
                    usuario.UserName ?? usuario.Email ?? string.Empty)
            };

            var identidad = new System.Security.Claims.ClaimsIdentity(
                claims,
                IdentityConstants.ApplicationScheme);

            var principal = new System.Security.Claims.ClaimsPrincipal(
                identidad);

            await httpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal);

            return Results.Ok(new
            {
                usuario.Id,
                usuario.Email,
                usuario.NombreCompleto
            });
        });

        grupo.MapGet("/me", (HttpContext context) =>
        {
            return Results.Ok(new
            {
                Autenticado = context.User.Identity?.IsAuthenticated ?? false,
                UsuarioId = context.User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
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
}

public sealed record LoginRequest(string Email, string Password);