using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Llama al login de la API. No guarda nada a mano: la cookie de sesión la guarda
/// el CookieContainer compartido (MauiProgram) y viaja sola en los pedidos siguientes.
/// </summary>
public sealed class AuthApi(HttpClient http) : IAuthApi
{
    public async Task<UsuarioLogin?> IniciarSesionAsync(string email, string password, CancellationToken ct = default)
    {
        using var respuesta = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password), ct);

        // 401 = email o contraseña incorrectos
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<UsuarioLogin>(ct);
    }

    public async Task<IReadOnlyList<Membresia>> ListarMembresiasAsync(CancellationToken ct = default)
    {
        var lista = await http.GetFromJsonAsync<List<Membresia>>("api/auth/membresias", ct);
        return lista ?? [];
    }

    public async Task<Membresia> SeleccionarOperadorAsync(Guid membresiaId, CancellationToken ct = default)
    {
        using var respuesta = await http.PostAsJsonAsync(
            "api/auth/seleccionar-operador", new SeleccionarOperadorRequest(membresiaId), ct);

        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<Membresia>(ct))!;
    }
}