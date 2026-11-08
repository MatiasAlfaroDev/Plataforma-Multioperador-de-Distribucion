using Microsoft.AspNetCore.Http;
using System.Net;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed class CookieAutenticacionHandler(
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var contexto = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "No hay una solicitud HTTP activa.");

        var cookie = contexto.Request.Cookies["UltimaMilla.Auth"];

        if (string.IsNullOrWhiteSpace(cookie))
        {
            throw new UnauthorizedAccessException(
                "No se encontró la cookie de autenticación.");
        }

        var cookies = new List<string>
        {
            $"UltimaMilla.Auth={WebUtility.UrlEncode(cookie)}"
        };

        // Cookie antiforgery específica de la solicitud a la API.
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

    public static readonly HttpRequestOptionsKey<string>
        CookieAntiforgeryKey = new("CookieAntiforgery");
}