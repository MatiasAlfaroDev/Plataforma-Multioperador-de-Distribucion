using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Seguridad;

/// <summary>
/// RF 37 ("por API"): el sistema del comercio se autentica con la clave del encabezado X-Api-Key en lugar
/// de la cookie. La clave es de un usuario del comercio y actúa con su perfil; solo se guarda su hash.
/// </summary>
public sealed class ClaveApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "ClaveApi";
    public const string Encabezado = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Encabezado, out var valor) || string.IsNullOrWhiteSpace(valor))
            return AuthenticateResult.NoResult();

        var hash = Hash(valor.ToString());

        var vinculo = await db.UsuariosComercios
            .AsNoTracking()
            .Where(uc => uc.ClaveApiHash == hash && uc.Activa && uc.Usuario.Activo)
            .Select(uc => new { uc.UsuarioId, uc.ComercioId, uc.Perfil, uc.Usuario.Email })
            .FirstOrDefaultAsync(Context.RequestAborted);

        if (vinculo is null)
            return AuthenticateResult.Fail("Clave de API inválida.");

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, vinculo.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, vinculo.Email ?? string.Empty),
            new Claim("comercio_id", vinculo.ComercioId.ToString()),
            new Claim("perfil_comercio", vinculo.Perfil),
            new Claim(ClaimTypes.Role, vinculo.Perfil)
        ], Esquema);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }

    public static string Hash(string clave) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clave.Trim())));

    /// <summary>
    /// Una llamada con clave de API no viene de un navegador: no hay cookie que falsificar,
    /// así que no lleva token antifalsificación.
    /// </summary>
    public static bool EsLlamadaConClave(HttpContext context) =>
        context.User.Identity?.AuthenticationType == Esquema;
}
