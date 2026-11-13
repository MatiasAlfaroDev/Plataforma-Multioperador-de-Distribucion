using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record PersonalItem(Guid Id, string Nombre, string Email, string Perfil);

public sealed record AltaPersonal(string NombreCompleto, string Email, string? Password, string Perfil, string? Telefono);

public sealed class PersonalApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<PersonalItem>> ListarAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<PersonalItem>>("api/personal", ct) ?? [];

    public async Task AltaAsync(AltaPersonal alta, CancellationToken ct = default)
    {
        using var respuestaToken = await http.GetAsync("api/antiforgery/token", ct);
        respuestaToken.EnsureSuccessStatusCode();
        var token = await respuestaToken.Content.ReadFromJsonAsync<TokenAntiforgery>(cancellationToken: ct);

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "api/personal")
        {
            Content = JsonContent.Create(alta)
        };
        solicitud.Headers.Add("X-CSRF-TOKEN", token?.Token);

        using var respuesta = await http.SendAsync(solicitud, ct);
        if (!respuesta.IsSuccessStatusCode)
            throw new HttpRequestException(await respuesta.Content.ReadAsStringAsync(ct), null, respuesta.StatusCode);
    }

    private sealed record TokenAntiforgery(string Token);
}
