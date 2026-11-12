
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed class RetirosApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<RetiroBackofficeDto>> ListarAsync(
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<RetiroBackofficeDto>>(
            "api/retiros",
            ct) ?? [];
    }

    public async Task<RetiroBackofficeDto?> ObtenerAsync(
        Guid retiroId,
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<RetiroBackofficeDto>(
            $"api/retiros/{retiroId}",
            ct);
    }

    public async Task RecibirEnDepositoAsync(
        Guid retiroId,
        CancellationToken ct = default)
    {
        await EjecutarAccionAsync(
            $"api/retiros/{retiroId}/recibir-deposito",
            ct);
    }

    public async Task EntregarAlComercioAsync(
        Guid retiroId,
        CancellationToken ct = default)
    {
        await EjecutarAccionAsync(
            $"api/retiros/{retiroId}/entregar-comercio",
            ct);
    }

    private async Task EjecutarAccionAsync(
        string ruta,
        CancellationToken ct)
    {
        using var respuestaToken = await http.GetAsync(
            "api/antiforgery/token", ct);

        respuestaToken.EnsureSuccessStatusCode();

        var datosToken = await respuestaToken.Content
            .ReadFromJsonAsync<TokenAntiforgeryRespuesta>(
                cancellationToken: ct);

        if (string.IsNullOrWhiteSpace(datosToken?.Token))
        {
            throw new InvalidOperationException(
                "No se pudo obtener el token de seguridad.");
        }

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post, ruta);

        solicitud.Headers.TryAddWithoutValidation(
            "X-CSRF-TOKEN",
            datosToken.Token);

        if (respuestaToken.Headers.TryGetValues(
            "Set-Cookie", out var cookies))
        {
            var cookieAntiforgery = cookies
                .Select(c => c.Split(';')[0])
                .FirstOrDefault(c =>
                    c.StartsWith(
                        ".AspNetCore.Antiforgery.",
                        StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(cookieAntiforgery))
            {
                solicitud.Options.Set(
                    CookieAutenticacionHandler.CookieAntiforgeryKey,
                    cookieAntiforgery);
            }
        }

        using var respuesta = await http.SendAsync(solicitud, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var contenido = await respuesta.Content
                .ReadAsStringAsync(ct);

            throw new HttpRequestException(
                $"No se pudo completar la operación: {contenido}",
                null,
                respuesta.StatusCode);
        }
    }

    private sealed record TokenAntiforgeryRespuesta(
        string Token);
}

public sealed class RetiroBackofficeDto
{
    public Guid Id { get; set; }
    public Guid OperadorId { get; set; }
    public Guid CuentaComercialId { get; set; }
    public string Motivo { get; set; } = "";

    public DireccionRetiroBackofficeDto DomicilioRetiro { get; set; } = new();
    public DireccionRetiroBackofficeDto DomicilioDestino { get; set; } = new();

    public DateOnly FechaSolicitada { get; set; }
    public TimeOnly HoraDesde { get; set; }
    public TimeOnly HoraHasta { get; set; }

    public string Estado { get; set; } = "";
    public DateTimeOffset SolicitadoEn { get; set; }

    public Guid? HojaDeRutaId { get; set; }
    public DateTimeOffset? RetiradoEn { get; set; }

    public string? ReferenciaFotografia { get; set; }
    public string? ReferenciaFirma { get; set; }

    public decimal PesoTotalKg { get; set; }
    public decimal VolumenTotalMetrosCubicos { get; set; }

    public Guid? VersionTarifariaId { get; set; }
    public decimal? TarifaCalculada { get; set; }

    public List<BultoRetiroBackofficeDto> Bultos { get; set; } = new();
}

public sealed class DireccionRetiroBackofficeDto
{
    public string Calle { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Localidad { get; set; } = "";
    public string Departamento { get; set; } = "";
}

public sealed class BultoRetiroBackofficeDto
{
    public Guid Id { get; set; }
    public string Identificacion { get; set; } = "";

    public decimal PesoKg { get; set; }

    public int AltoCm { get; set; }
    public int AnchoCm { get; set; }
    public int ProfundidadCm { get; set; }

    public decimal VolumenMetrosCubicos { get; set; }
}
