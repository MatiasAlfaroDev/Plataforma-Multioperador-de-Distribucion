using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>Llama al login de la API. Devuelve null si el email o la contraseña están mal.</summary>
public sealed class AuthApi(HttpClient http) : IAuthApi
{
    public async Task<LoginResponse?> IniciarSesionAsync(string email, string password, CancellationToken ct = default)
    {
        // 1. Armar lo que se manda
        var pedido = new LoginRequest(email, password, "OPERADOR");            // ✏️ el contexto

        // 2. Mandarlo por POST a la dirección del login
        using var respuesta = await http.PostAsJsonAsync("api/auth/login", pedido, ct);   // ✏️ la dirección

        // 3. 401 = email o contraseña incorrectos
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        // 4. Cualquier otro error, que se note
        respuesta.EnsureSuccessStatusCode();

        // 5. Convertir el JSON de la respuesta a nuestro record
        return await respuesta.Content.ReadFromJsonAsync<LoginResponse>(ct);   // ✏️ lo que devuelve
    }
}