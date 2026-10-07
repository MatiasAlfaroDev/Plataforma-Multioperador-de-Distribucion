using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed class TarifasApiClient(HttpClient http)
{
    public async Task<TarifaVigenteItem?> ObtenerVigenteAsync(
        Guid operadorId,
        Guid zonaCoberturaId,
        CancellationToken ct = default)
    {
        var url =
            $"/api/tarifas/vigente?operadorId={operadorId}&zonaCoberturaId={zonaCoberturaId}";

        var response = await http.GetAsync(url, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TarifaVigenteItem>(
            cancellationToken: ct);
    }
    public async Task ActualizarAsync(
        ActualizarTarifaRequest request,
        CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            "/api/tarifas",
            request,
            ct);

        response.EnsureSuccessStatusCode();
    }
    public async Task<IReadOnlyList<ZonaItem>> ListarZonasAsync(
        Guid operadorId,
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<ZonaItem>>(
            $"api/zonas?operadorId={operadorId}",
            ct) ?? [];
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
    DateTimeOffset VigenciaDesde);

public sealed record ActualizarTarifaRequest(
    Guid OperadorId,
    Guid CuadroTarifarioId,
    Guid ZonaCoberturaId,
    decimal PrecioBase,
    decimal PrecioPorKg,
    decimal PrecioPorMetroCubico,
    decimal RecargoUrgentePorcentaje,
    decimal BonificacionPorcentaje);

public sealed record ZonaItem(
    Guid Id,
    string Nombre,
    string Departamento);