using System.Net;
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed class TarifasApiClient(HttpClient http)
{
    public async Task<TarifaVigenteItem?> ObtenerVigenteAsync(
        Guid operadorId,
        Guid zonaCoberturaId,
        string tipoServicio,
        CancellationToken ct = default)
    {
        var url =
            $"/api/tarifas/vigente?operadorId={operadorId}" +
            $"&zonaCoberturaId={zonaCoberturaId}" +
            $"&tipoServicio={Uri.EscapeDataString(tipoServicio)}";

        using var response = await http.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<TarifaVigenteItem>(
                cancellationToken: ct);
    }

    public Task ActualizarAsync(
        ActualizarTarifaRequest request,
        CancellationToken ct = default)
    {
        return EnviarTarifaAsync(
            "/api/tarifas",
            request,
            ct);
    }

    public Task CrearAsync(
        CrearTarifaRequest request,
        CancellationToken ct = default)
    {
        return EnviarTarifaAsync(
            "/api/tarifas/nueva",
            request,
            ct);
    }

    public async Task<IReadOnlyList<ZonaItem>> ListarZonasAsync(
        Guid operadorId,
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<ZonaItem>>(
            $"api/zonas?operadorId={operadorId}",
            ct) ?? [];
    }

    private sealed record AntiforgeryTokenResponse(
        string Token);

    private sealed record AntiforgeryData(
        string Token,
        string Cookie);

    private async Task<AntiforgeryData> ObtenerTokenAsync(
        CancellationToken ct)
    {
        using var response = await http.GetAsync(
            "/api/antiforgery/token",
            ct);

        response.EnsureSuccessStatusCode();

        var resultado = await response.Content
            .ReadFromJsonAsync<AntiforgeryTokenResponse>(
                cancellationToken: ct);

        if (string.IsNullOrWhiteSpace(resultado?.Token))
        {
            throw new InvalidOperationException(
                "No se pudo obtener el token de seguridad.");
        }

        if (!response.Headers.TryGetValues(
                "Set-Cookie",
                out var cookies))
        {
            throw new InvalidOperationException(
                "La API no devolvió la cookie antiforgery.");
        }

        string? cookieAntiforgery = null;

        foreach (var cookie in cookies)
        {
            var valor = cookie.Split(';', 2)[0].Trim();

            if (valor.StartsWith(
                    ".AspNetCore.Antiforgery.",
                    StringComparison.Ordinal))
            {
                cookieAntiforgery = valor;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(cookieAntiforgery))
        {
            throw new InvalidOperationException(
                "No se encontró la cookie antiforgery.");
        }

        return new AntiforgeryData(
            resultado.Token,
            cookieAntiforgery);
    }

    private async Task EnviarTarifaAsync<T>(
        string ruta,
        T request,
        CancellationToken ct)
    {
        var seguridad = await ObtenerTokenAsync(ct);

        using var mensaje = new HttpRequestMessage(
            HttpMethod.Post,
            ruta)
        {
            Content = JsonContent.Create(request)
        };

        mensaje.Headers.TryAddWithoutValidation(
            "X-CSRF-TOKEN",
            seguridad.Token);

        // El CookieAutenticacionHandler agregará
        // la cookie de autenticación a esta cookie.
        mensaje.Headers.TryAddWithoutValidation(
            "Cookie",
            seguridad.Cookie);

        using var response = await http.SendAsync(
            mensaje,
            ct);

        response.EnsureSuccessStatusCode();
    }
}

public sealed record TarifaVigenteItem(
    Guid VersionTarifariaId,
    Guid CuadroTarifarioId,
    Guid ZonaCoberturaId,
    decimal PrecioBase,
    decimal PrecioPorKg,
    decimal PrecioPorMetroCubico,
    decimal RecargoUrgentePorcentaje,
    decimal BonificacionPorcentaje,
    DateTimeOffset VigenciaDesde,
    string TipoServicio);

public sealed record ActualizarTarifaRequest(
    Guid OperadorId,
    Guid CuadroTarifarioId,
    Guid ZonaCoberturaId,
    decimal PrecioBase,
    decimal PrecioPorKg,
    decimal PrecioPorMetroCubico,
    decimal RecargoUrgentePorcentaje,
    decimal BonificacionPorcentaje,
    string TipoServicio);

public sealed record ZonaItem(
    Guid Id,
    string Nombre,
    string Departamento);

public sealed record CrearTarifaRequest(
    Guid OperadorId,
    Guid ZonaCoberturaId,
    string Nombre,
    string TipoServicio,
    decimal PrecioBase,
    decimal PrecioPorKg,
    decimal PrecioPorMetroCubico,
    decimal RecargoUrgentePorcentaje,
    decimal BonificacionPorcentaje);