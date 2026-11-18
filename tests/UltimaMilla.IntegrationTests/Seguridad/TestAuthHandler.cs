
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UltimaMilla.IntegrationTests.Seguridad;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string Esquema = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                "X-Test-Usuario", out var usuario))
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.ToString()),
            new(ClaimTypes.Name, "Usuario de prueba")
        };

        if (Request.Headers.TryGetValue(
                "X-Test-Rol", out var rol))
        {
            claims.Add(new Claim(
                ClaimTypes.Role, rol.ToString()));
        }

        if (Request.Headers.TryGetValue(
                "X-Test-Operador", out var operador))
        {
            claims.Add(new Claim(
                "operador_id", operador.ToString()));
        }

        var identidad = new ClaimsIdentity(
            claims, Esquema);

        var principal = new ClaimsPrincipal(identidad);

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(
                    principal, Esquema)));
    }
}
