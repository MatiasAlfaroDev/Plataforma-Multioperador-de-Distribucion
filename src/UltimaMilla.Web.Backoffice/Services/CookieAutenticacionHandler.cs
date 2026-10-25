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

        request.Headers.TryAddWithoutValidation(
            "Cookie",
            $"UltimaMilla.Auth={WebUtility.UrlEncode(cookie)}");

        return await base.SendAsync(request, cancellationToken);
    }
}