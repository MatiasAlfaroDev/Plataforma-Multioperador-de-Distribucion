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

        var cookieAutenticacion =
            contexto.Request.Cookies["UltimaMilla.Auth"];

        if (string.IsNullOrWhiteSpace(cookieAutenticacion))
        {
            throw new UnauthorizedAccessException(
                "No se encontró la cookie de autenticación.");
        }

        // Conservamos cualquier cookie específica
        // que el cliente haya agregado a la solicitud.
        string cookiesExistentes = string.Empty;

        if (request.Headers.TryGetValues(
                "Cookie",
                out var valores))
        {
            cookiesExistentes = string.Join("; ", valores);
        }

        var cookieSesion =
            $"UltimaMilla.Auth={WebUtility.UrlEncode(cookieAutenticacion)}";

        var cookiesCompletas =
            string.IsNullOrWhiteSpace(cookiesExistentes)
                ? cookieSesion
                : $"{cookieSesion}; {cookiesExistentes}";

        request.Headers.Remove("Cookie");

        request.Headers.TryAddWithoutValidation(
            "Cookie",
            cookiesCompletas);

        return await base.SendAsync(
            request,
            cancellationToken);
    }
}