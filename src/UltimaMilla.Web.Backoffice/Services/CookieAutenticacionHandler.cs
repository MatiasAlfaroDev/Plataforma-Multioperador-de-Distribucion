
using System.Net;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed class CookieAutenticacionHandler(
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<string>
        CookieAntiforgeryKey = new("CookieAntiforgery");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var contexto = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "No hay una solicitud HTTP activa.");

        var cookieAutenticacion =
            contexto.Request.Cookies["UltimaMilla.Auth"];

        if (string.IsNullOrWhiteSpace(cookieAutenticacion))
        {
            throw new UnauthorizedAccessException(
                "No se encontró la cookie de autenticación.");
        }

        var cookies = new List<string>
        {
            $"UltimaMilla.Auth={WebUtility.UrlEncode(cookieAutenticacion)}"
        };

        // Conservar las cookies agregadas por otros clientes HTTP.
        if (request.Headers.TryGetValues("Cookie", out var existentes))
        {
            foreach (var valor in existentes)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                    cookies.Add(valor);
            }
        }

        // Agregar la cookie antiforgery utilizada por la API.
        if (request.Options.TryGetValue(
                CookieAntiforgeryKey,
                out string? cookieAntiforgery) &&
            !string.IsNullOrWhiteSpace(cookieAntiforgery))
        {
            cookies.Add(cookieAntiforgery);
        }

        request.Headers.Remove("Cookie");

        request.Headers.TryAddWithoutValidation(
            "Cookie",
            string.Join("; ", cookies));

        return await base.SendAsync(request, cancellationToken);
    }
}
