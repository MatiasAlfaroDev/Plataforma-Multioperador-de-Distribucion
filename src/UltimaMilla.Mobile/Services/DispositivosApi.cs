using System.Net.Http.Json;

namespace UltimaMilla.Mobile.Services;

/// <summary>Registro del teléfono en la API para recibir avisos push.</summary>
public interface IDispositivosApi
{
    Task RegistrarAsync(string token, string plataforma, CancellationToken ct = default);
}

/// <summary>Usa la misma bolsa de cookies que el login, así la API sabe de qué usuario es el teléfono.</summary>
public sealed class DispositivosApi(HttpClient http) : IDispositivosApi
{
    public async Task RegistrarAsync(string token, string plataforma, CancellationToken ct = default)
    {
        using var respuesta = await http.PostAsJsonAsync(
            "api/dispositivos", new RegistrarDispositivoRequest(token, plataforma), ct);

        respuesta.EnsureSuccessStatusCode();
    }

    private sealed record RegistrarDispositivoRequest(string Token, string Plataforma);
}
