using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>Implementación HTTP de IEnviosApi (mismo estilo que EventsApi de la demo).</summary>
public class EnviosApi : IEnviosApi
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EnviosApi(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<IReadOnlyList<OperadorItem>> ListarOperadoresAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<OperadorItem>>("api/operadores", JsonOptions, ct) ?? [];

    public async Task<IReadOnlyList<EnvioItem>> ListarEnviosAsync(Guid operadorId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<EnvioItem>>($"api/envios?operadorId={operadorId}", JsonOptions, ct) ?? [];

    /// <summary>
    /// POST /api/envios/{envioId}/eventos-movil. El contrato está en LEEME-OFFLINE.md
    /// para quien implemente el endpoint en la API.
    /// </summary>
    public async Task<(ResultadoEnvio Resultado, string? Detalle)> EnviarEventoAsync(EventoPendiente evento, CancellationToken ct = default)
    {
        var cuerpo = new
        {
            idEvento = evento.Id,
            tipo = evento.Tipo,
            motivo = evento.Motivo,
            receptor = evento.Receptor,
            fechaHora = evento.FechaHora
        };

        try
        {
            using var respuesta = await _http.PostAsJsonAsync($"api/envios/{evento.EnvioId}/eventos-movil", cuerpo, ct);

            // 2xx = guardado. 409 = el servidor ya tenía ese idEvento: también es éxito (idempotencia).
            if (respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.Conflict)
                return (ResultadoEnvio.Aceptado, null);

            var detalle = await respuesta.Content.ReadAsStringAsync(ct);
            return (ResultadoEnvio.Rechazado, $"{(int)respuesta.StatusCode}: {Recortar(detalle)}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (ResultadoEnvio.SinConexion, ex.Message);
        }
    }

    private static string Recortar(string texto) => texto.Length <= 200 ? texto : texto[..200];
}
