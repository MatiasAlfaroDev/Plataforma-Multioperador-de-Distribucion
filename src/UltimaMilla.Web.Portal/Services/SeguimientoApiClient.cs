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
    IReadOnlyList<MovimientoSeguimientoDto> Historial,
    int BultosTotales = 0,
    int BultosEntregados = 0,
    string Tipo = "Envio",
    string? OperadorNombre = null,
    string? OperadorColorPrimario = null);

public sealed record DisponibilidadReprogramacionPublicaDto(
    bool PuedeSolicitar,
    string? Motivo);    

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

    public async Task<DisponibilidadReprogramacionPublicaDto?>
    ConsultarDisponibilidadAsync(
        string token,
        CancellationToken ct = default)
{
    using var respuesta = await http.GetAsync(
        $"api/seguimiento/{Uri.EscapeDataString(token)}/reprogramacion/disponibilidad",
        ct);

    if (respuesta.StatusCode == HttpStatusCode.NotFound)
        return null;

    respuesta.EnsureSuccessStatusCode();

    return await respuesta.Content
        .ReadFromJsonAsync<DisponibilidadReprogramacionPublicaDto>(
            cancellationToken: ct);
}

public async Task<bool> SolicitarReprogramacionAsync(
    string token,
    CancellationToken ct = default)
{
    using var respuesta = await http.PostAsync(
        $"api/seguimiento/{Uri.EscapeDataString(token)}/reprogramacion",
        content: null,
        cancellationToken: ct);

    return respuesta.IsSuccessStatusCode;
}
    
}