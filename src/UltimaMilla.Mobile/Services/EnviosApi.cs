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
}
