
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record ConfiguracionOperadorDto(
    Guid Id,
    string Nombre,
    string ColorPrimario);

public sealed class ConfiguracionOperadorApiClient(HttpClient http)
{
    public async Task<ConfiguracionOperadorDto?> ObtenerAsync(
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<ConfiguracionOperadorDto>(
            "api/operadores/mi-operador", ct);
    }

    public async Task ActualizarColorAsync(
        string colorPrimario,
        CancellationToken ct = default)
    {
        using var respuestaToken = await http.GetAsync(
            "api/antiforgery/token", ct);

        respuestaToken.EnsureSuccessStatusCode();

        var token = await respuestaToken.Content
            .ReadFromJsonAsync<TokenAntiforgery>(
                cancellationToken: ct);

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Put,
            "api/operadores/mi-operador/color")
        {
            Content = JsonContent.Create(new
            {
                ColorPrimario = colorPrimario
            })
        };

        solicitud.Headers.Add("X-CSRF-TOKEN", token?.Token);

        // La API valida el token contra su cookie antifalsificación: hay que reenviarla, como en HojasDeRutaApiClient.
        if (respuestaToken.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var cookieAntiforgery = cookies
                .Select(c => c.Split(';')[0])
                .FirstOrDefault(c => c.StartsWith(
                    ".AspNetCore.Antiforgery.", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(cookieAntiforgery))
                solicitud.Options.Set(CookieAutenticacionHandler.CookieAntiforgeryKey, cookieAntiforgery);
        }

        using var respuesta = await http.SendAsync(solicitud, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                await respuesta.Content.ReadAsStringAsync(ct),
                null,
                respuesta.StatusCode);
        }
    }

    private sealed record TokenAntiforgery(string Token);
}
