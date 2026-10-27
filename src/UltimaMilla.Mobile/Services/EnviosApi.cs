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
        var esEntrega = evento.Tipo == "Entregado";
        var cuerpo = new
        {
            idEvento = evento.Id,
            tipo = evento.Tipo,
            motivo = evento.Motivo,
            receptor = evento.Receptor,
            fechaHora = evento.FechaHora,
            latitud = evento.Latitud,
            longitud = evento.Longitud,
            // La misma foto es prueba de entrega o evidencia del intento fallido, según el tipo.
            referenciaFotografia = esEntrega ? evento.ReferenciaFoto : null,
            evidenciaFallo = esEntrega ? null : evento.ReferenciaFoto
        };

        try
        {
            using var respuesta = await _http.PostAsJsonAsync($"api/envios/{evento.EnvioId}/eventos-movil", cuerpo, ct);

            // 2xx = guardado. 409 = el servidor ya tenía ese idEvento: también es éxito (idempotencia).
            if (respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.Conflict)
                return (ResultadoEnvio.Aceptado, null);

            return (ResultadoEnvio.Rechazado, await LeerMotivoAsync(respuesta, ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (ResultadoEnvio.SinConexion, ex.Message);
        }
    }

    /// <summary>
    /// La API explica el rechazo en un ProblemDetails: { "title": "El envío debe estar en tránsito..." }.
    /// Se guarda solo ese texto, que es el que ve el repartidor.
    /// </summary>
    private static async Task<string> LeerMotivoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            return "Sesión vencida: iniciá sesión de nuevo.";

        var texto = await respuesta.Content.ReadAsStringAsync(ct);
        try
        {
            using var json = JsonDocument.Parse(texto);
            if (json.RootElement.TryGetProperty("title", out var titulo) && titulo.GetString() is { Length: > 0 } motivo)
                return motivo;
        }
        catch (JsonException)
        {
            // No era JSON: se guarda el texto tal cual.
        }
        return $"Error {(int)respuesta.StatusCode}: {Recortar(texto)}";
    }
    private static string Recortar(string texto) => texto.Length <= 200 ? texto : texto[..200];
}
