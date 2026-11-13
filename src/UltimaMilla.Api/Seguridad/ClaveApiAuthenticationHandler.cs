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
/// También acepta la clave del ambiente de pruebas (ver <see cref="ClavesDePruebas"/>), que se marca en la
/// identidad con el claim <see cref="ClaveEntorno"/> para que la API pública no guarde nada.
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
    public const string ClaveEntorno = "entorno_api";
    public const string EntornoPruebas = "pruebas";
    public const string EntornoProduccion = "produccion";

    private sealed record Vinculo(Guid UsuarioId, Guid ComercioId, string Perfil, string? Email);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Encabezado, out var valor) || string.IsNullOrWhiteSpace(valor))
            return AuthenticateResult.NoResult();

        var clave = valor.ToString().Trim();
        var pruebas = ClavesDePruebas.EsClaveDePruebas(clave);

        var vinculo = pruebas
            ? await BuscarPorClaveDePruebasAsync(clave)
            : await BuscarPorClaveRealAsync(clave);

        if (vinculo is null)
            return AuthenticateResult.Fail("Clave de API inválida.");

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, vinculo.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, vinculo.Email ?? string.Empty),
            new Claim("comercio_id", vinculo.ComercioId.ToString()),
            new Claim("perfil_comercio", vinculo.Perfil),
            new Claim(ClaimTypes.Role, vinculo.Perfil),
            new Claim(ClaveEntorno, pruebas ? EntornoPruebas : EntornoProduccion)
        ], Esquema);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }

    private async Task<Vinculo?> BuscarPorClaveRealAsync(string clave)
    {
        var hash = Hash(clave);

        return await db.UsuariosComercios
            .AsNoTracking()
            .Where(uc => uc.ClaveApiHash == hash && uc.Activa && uc.Usuario.Activo)
            .Select(uc => new Vinculo(uc.UsuarioId, uc.ComercioId, uc.Perfil, uc.Usuario.Email))
            .FirstOrDefaultAsync(Context.RequestAborted);
    }

    private async Task<Vinculo?> BuscarPorClaveDePruebasAsync(string clave)
    {
        if (!ClavesDePruebas.TryLeer(clave, out var vinculoId, out var firma))
            return null;

        var datos = await db.UsuariosComercios
            .AsNoTracking()
            .Where(uc => uc.Id == vinculoId && uc.Activa && uc.Usuario.Activo && uc.ClaveApiHash != null)
            .Select(uc => new
            {
                uc.ClaveApiHash,
                Vinculo = new Vinculo(uc.UsuarioId, uc.ComercioId, uc.Perfil, uc.Usuario.Email)
            })
            .FirstOrDefaultAsync(Context.RequestAborted);

        return datos is not null && ClavesDePruebas.EsValida(vinculoId, firma, datos.ClaveApiHash!)
            ? datos.Vinculo
            : null;
    }

    public static string Hash(string clave) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clave.Trim())));

    /// <summary>
    /// Una llamada con clave de API no viene de un navegador: no hay cookie que falsificar,
    /// así que no lleva token antifalsificación.
    /// </summary>
    public static bool EsLlamadaConClave(HttpContext context) =>
        context.User.Identity?.AuthenticationType == Esquema;

    /// <summary>La llamada usa la clave del ambiente de pruebas: no debe guardar nada.</summary>
    public static bool EsEntornoDePruebas(HttpContext context) =>
        EsLlamadaConClave(context) &&
        context.User.FindFirst(ClaveEntorno)?.Value == EntornoPruebas;
}