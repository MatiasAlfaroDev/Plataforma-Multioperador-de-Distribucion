using System.Net;
using System.Net.Http.Json;

namespace UltimaMilla.Web.Portal.Services;

public sealed record MovimientoSeguimientoDto(
    string Estado,
    DateTimeOffset Fecha);

public sealed record SeguimientoPublicoDto(
    string Referencia,
    string Estado,
    DateTimeOffset FechaAlta,
    DateOnly? FechaEntregaComprometida,
    TimeOnly? HoraEntregaDesde,
    TimeOnly? HoraEntregaHasta,
    DateTimeOffset? FechaReprogramada,
    string? FranjaReprogramada,
    IReadOnlyList<MovimientoSeguimientoDto> Historial);

public sealed class SeguimientoApiClient(HttpClient http)
{
    public async Task<SeguimientoPublicoDto?> ConsultarAsync(
        string token,
        CancellationToken ct = default)
    {
        using var respuesta = await http.GetAsync(
            $"api/seguimiento/{Uri.EscapeDataString(token)}",
            ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;

        respuesta.EnsureSuccessStatusCode();

        return await respuesta.Content
            .ReadFromJsonAsync<SeguimientoPublicoDto>(
                cancellationToken: ct);
    }
}