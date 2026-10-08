
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UltimaMilla.Infrastructure.Identity;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Auth;

public static class AuthEndpoints
{
    // Datos que recibimos al iniciar sesión.
    public record LoginRequest(
        string Email,
        string Password,
        Guid? OperadorId);

    // Registra la ruta POST /api/auth/login.
    public static RouteGroupBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth")
            .WithTags("Autenticación");

        grupo.MapPost("/login", LoginAsync)
            .AllowAnonymous();

        return grupo;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IConfiguration configuration)
    {
        // 1. Validar que ingresaron correo y contraseña.
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new
            {
                mensaje = "Correo y contraseña son obligatorios."
            });
        }

        // 2. Buscar al usuario mediante Identity.
        var usuario = await userManager.FindByEmailAsync(
            request.Email.Trim());

        if (usuario is null || !usuario.Activo)
            return Results.Unauthorized();

        // 3. Comprobar si la cuenta está bloqueada.
        if (await userManager.IsLockedOutAsync(usuario))
            return Results.Unauthorized();

        // 4. Validar la contraseña.
        var passwordValida = await userManager.CheckPasswordAsync(
            usuario,
            request.Password);

        if (!passwordValida)
        {
            await userManager.AccessFailedAsync(usuario);
            return Results.Unauthorized();
        }

        // 5. Buscar los operadores a los que pertenece.
        var operadores = await db.UsuariosOperadores
            .AsNoTracking()
            .Where(x => x.UsuarioId == usuario.Id)
            .ToListAsync();

        // Si tiene un solo operador, lo seleccionamos.
        // Si tiene varios, debe indicar cuál quiere utilizar.
        var pertenencia = request.OperadorId.HasValue
            ? operadores.FirstOrDefault(x =>
                x.OperadorId == request.OperadorId.Value)
            : operadores.Count == 1
                ? operadores[0]
                : null;

        if (pertenencia is null)
        {
            return Results.Json(new
            {
                mensaje = "No se pudo determinar un operador autorizado."
            },
            statusCode: StatusCodes.Status403Forbidden);
        }

        // 6. Reiniciar el contador de intentos fallidos.
        await userManager.ResetAccessFailedCountAsync(usuario);

        // 7. Crear los claims del usuario autenticado.
        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                usuario.Id.ToString()),

            new(
                JwtRegisteredClaimNames.Email,
                usuario.Email ?? request.Email),

            new(
                "operador_id",
                pertenencia.OperadorId.ToString()),

            new(
                ClaimTypes.Role,
                pertenencia.Rol),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        // 8. Obtener la configuración JWT de User Secrets.
        var clave = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Key.");

        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Issuer.");

        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Audience.");

        // 9. Firmar el token.
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(clave)),
            SecurityAlgorithms.HmacSha256);

        var expiracion = DateTime.UtcNow.AddHours(2);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiracion,
            signingCredentials: credenciales);

        // 10. Devolver el token y los datos del usuario.
        return Results.Ok(new
        {
            accessToken = new JwtSecurityTokenHandler()
                .WriteToken(token),

            tokenType = "Bearer",
            expiresAt = expiracion,

            usuario = new
            {
                usuario.Id,
                usuario.Email,
                usuario.NombreCompleto,
                pertenencia.OperadorId,
                pertenencia.Rol
            }
        });
    }
}
