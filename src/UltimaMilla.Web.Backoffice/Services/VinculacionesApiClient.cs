
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record SolicitudComercialItem(
    Guid Id,
    Guid ComercioId,
    string NombreComercio,
    DateTimeOffset CreadoEn
);

public sealed class VinculacionesApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<SolicitudComercialItem>> ListarPendientesAsync(
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<SolicitudComercialItem>>(
            "api/vinculaciones/pendientes", ct) ?? [];
    }

    public async Task AceptarAsync(Guid id, CancellationToken ct = default)
    {
        await EjecutarAccionAsync(id, "aceptar", ct);
    }

    public async Task RechazarAsync(Guid id, CancellationToken ct = default)
    {
        await EjecutarAccionAsync(id, "rechazar", ct);
    }

    private async Task EjecutarAccionAsync(
        Guid id,
        string accion,
        CancellationToken ct)
    {
        // Obtener el token antiforgery de la API.
        using var respuestaToken = await http.GetAsync(
            "api/antiforgery/token", ct);

        await VerificarRespuestaAsync(respuestaToken, ct);

        var datosToken = await respuestaToken.Content
            .ReadFromJsonAsync<TokenAntiforgery>(cancellationToken: ct);

        if (string.IsNullOrWhiteSpace(datosToken?.Token))
            throw new InvalidOperationException(
                "La API no devolvió un token de seguridad.");

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/vinculaciones/{id}/{accion}");

        solicitud.Headers.Add("X-CSRF-TOKEN", datosToken.Token);

        using var respuesta = await http.SendAsync(solicitud, ct);

        await VerificarRespuestaAsync(respuesta, ct);
    }

    private static async Task VerificarRespuestaAsync(
        HttpResponseMessage respuesta,
        CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var detalle = await respuesta.Content.ReadAsStringAsync(ct);

        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(detalle)
                ? $"La API respondió HTTP {(int)respuesta.StatusCode}."
                : detalle,
            null,
            respuesta.StatusCode);
    }

    private sealed record TokenAntiforgery(string Token);
}
