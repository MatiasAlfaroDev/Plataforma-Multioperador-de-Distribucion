using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>Copia de lo que devuelve GET /api/admin/banderas (aclaración 3.2).</summary>
public sealed record BanderasOperadorItem(Guid OperadorId, string Operador, Dictionary<string, bool> Banderas);

public sealed record CambioBanderaItem(string Nombre, Guid OperadorId, bool Habilitada, string ModificadoPor, DateTimeOffset ModificadoEn);

public sealed record BanderasAdminItem(List<BanderasOperadorItem> Estado, List<CambioBanderaItem> Historial);

public sealed class BanderasApiClient(HttpClient http)
{
    public async Task<BanderasAdminItem> ListarAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<BanderasAdminItem>("api/admin/banderas", ct) ?? new([], []);

    public async Task CambiarAsync(Guid operadorId, string bandera, bool habilitada, CancellationToken ct = default)
    {
        using var respuestaToken = await http.GetAsync("api/antiforgery/token", ct);
        respuestaToken.EnsureSuccessStatusCode();
        var token = await respuestaToken.Content.ReadFromJsonAsync<TokenAntiforgery>(cancellationToken: ct);

        using var solicitud = new HttpRequestMessage(HttpMethod.Put, $"api/admin/banderas/{operadorId}/{Uri.EscapeDataString(bandera)}")
        {
            Content = JsonContent.Create(new { habilitada })
        };
        solicitud.Headers.Add("X-CSRF-TOKEN", token?.Token);

        using var respuesta = await http.SendAsync(solicitud, ct);
        if (!respuesta.IsSuccessStatusCode)
            throw new HttpRequestException(await respuesta.Content.ReadAsStringAsync(ct), null, respuesta.StatusCode);
    }

    private sealed record TokenAntiforgery(string Token);
}
