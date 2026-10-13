
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Auth.Commands.Login;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Identity;

public sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IConfiguration configuration) : IAuthenticationService
{
    public async Task<LoginResult> LoginAsync(
        LoginCommand command,
        CancellationToken ct)
    {
        // 1. Buscar el usuario mediante Identity.
        var usuario = await userManager.FindByEmailAsync(
            command.Email.Trim());

        if (usuario is null || !usuario.Activo)
            return new LoginResult(LoginStatus.CredencialesInvalidas);

        // 2. Verificar si está bloqueado.
        if (await userManager.IsLockedOutAsync(usuario))
            return new LoginResult(LoginStatus.CredencialesInvalidas);

        // 3. Validar la contraseña.
        var passwordValida = await userManager.CheckPasswordAsync(
            usuario,
            command.Password);

        if (!passwordValida)
        {
            await userManager.AccessFailedAsync(usuario);

            return new LoginResult(LoginStatus.CredencialesInvalidas);
        }

        // 4. Identificar el contexto de trabajo.
        var tipoContexto = command.TipoContexto?.Trim().ToUpperInvariant();

        if (tipoContexto is not ("OPERADOR" or "COMERCIO"))
            return new LoginResult(LoginStatus.TipoContextoInvalido);

        Guid? operadorId = null;
        Guid? comercioId = null;
        string rol;

        // 5. Validar la pertenencia del usuario.
        if (tipoContexto == "OPERADOR")
        {
            if (command.ComercioId.HasValue)
                return new LoginResult(LoginStatus.TipoContextoInvalido);

            var operadores = await db.UsuariosOperadores
                .AsNoTracking()
                .Where(x => x.UsuarioId == usuario.Id)
                .ToListAsync(ct);

            var pertenencia = command.OperadorId.HasValue
                ? operadores.FirstOrDefault(x =>
                    x.OperadorId == command.OperadorId.Value)
                : operadores.Count == 1
                    ? operadores[0]
                    : null;

            if (pertenencia is null)
                return new LoginResult(LoginStatus.OperadorNoAutorizado);

            operadorId = pertenencia.OperadorId;
            rol = pertenencia.Rol;
        }
        else
        {
            if (command.OperadorId.HasValue)
                return new LoginResult(LoginStatus.TipoContextoInvalido);

            var comercios = await db.UsuariosComercios
                .AsNoTracking()
                .Where(x => x.UsuarioId == usuario.Id)
                .ToListAsync(ct);

            var pertenencia = command.ComercioId.HasValue
                ? comercios.FirstOrDefault(x =>
                    x.ComercioId == command.ComercioId.Value)
                : comercios.Count == 1
                    ? comercios[0]
                    : null;

            if (pertenencia is null)
                return new LoginResult(LoginStatus.ComercioNoAutorizado);

            comercioId = pertenencia.ComercioId;
            rol = pertenencia.Rol;
        }

        // 6. Reiniciar intentos fallidos.
        await userManager.ResetAccessFailedCountAsync(usuario);

        // Crear los claims del JWT.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email,
                usuario.Email ?? command.Email),
            new("tipo_contexto", tipoContexto),
            new(ClaimTypes.Role, rol),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (operadorId.HasValue)
        {
            claims.Add(new Claim(
                "operador_id",
                operadorId.Value.ToString()));
        }

        if (comercioId.HasValue)
        {
            claims.Add(new Claim(
                "comercio_id",
                comercioId.Value.ToString()));
        }

        // 7. Obtener la configuración JWT.
        var clave = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Key.");

        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Issuer.");

        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "Falta configurar Jwt:Audience.");

        // 8. Firmar el token.
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

        // 9. Construir la respuesta.
        var response = new LoginResponse(
            AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
            TokenType: "Bearer",
            ExpiresAt: expiracion,
            Usuario: new UsuarioLoginDto(
            usuario.Id,
            usuario.Email ?? command.Email,
            usuario.NombreCompleto,
            operadorId,
            rol,
            tipoContexto,
            comercioId));

        return new LoginResult(LoginStatus.Exitoso, response);
    }
}
