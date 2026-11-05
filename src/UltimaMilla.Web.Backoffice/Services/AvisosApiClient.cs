namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>Copia de lo que devuelve GET /api/avisos.</summary>
public sealed record AvisoItem(
    Guid Id,
    Guid CuentaComercialId,
    string TipoEvento,
    string ClaveEvento,
    string Estado,
    int Intentos,
    string? UltimoError,
    DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoIntentoEn,
    DateTimeOffset? EntregadaEn);

public sealed class AvisosApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<AvisoItem>> ListarAsync(
        string? estado,
        CancellationToken ct = default)
    {
        var url = "api/avisos" + (string.IsNullOrWhiteSpace(estado) ? "" : $"?estado={Uri.EscapeDataString(estado)}");
        return await http.GetFromJsonAsync<List<AvisoItem>>(url, ct) ?? [];
    }

    public async Task ReenviarAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var respuesta = await http.PostAsync($"api/avisos/{id}/reenviar", null, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(ct);

            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(detalle)
                    ? $"La API rechazó el reenvío (HTTP {(int)respuesta.StatusCode})."
                    : $"La API respondió: {detalle}",
                null,
                respuesta.StatusCode);
        }
    }
}
