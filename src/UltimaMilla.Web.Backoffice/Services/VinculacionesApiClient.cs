
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record SolicitudComercialItem(
    Guid Id,
    Guid ComercioId,
    string NombreComercio,
    DateTimeOffset CreadoEn
);

// Estado: 0 Pendiente, 1 Confirmada, 2 Rechazada (EstadoCuentaComercial serializado como número).
public sealed record CuentaComercialItem(
    Guid Id,
    Guid ComercioId,
    string ComercioNombre,
    string? ComercioRut,
    bool Activa,
    int Estado,
    string? DireccionRecepcion)
{
    public bool Aceptada => Estado == 1;
}

public sealed class VinculacionesApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<SolicitudComercialItem>> ListarPendientesAsync(
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<SolicitudComercialItem>>(
            "api/vinculaciones/pendientes", ct) ?? [];
    }

    // Comercios del operador de la sesión (RF 1); la página se queda con los aceptados.
    public async Task<IReadOnlyList<CuentaComercialItem>> ListarCuentasAsync(
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<CuentaComercialItem>>(
            "api/cuentas-comerciales", ct) ?? [];
    }

    public Task ModificarComercioAsync(Guid cuentaId, string nombre, CancellationToken ct = default) =>
        EnviarAsync(HttpMethod.Put, $"api/vinculaciones/{cuentaId}/comercio", new { Nombre = nombre }, ct);

    public Task CambiarEstadoAsync(Guid cuentaId, bool activa, CancellationToken ct = default) =>
        EnviarAsync(HttpMethod.Put, $"api/vinculaciones/{cuentaId}/estado", new { Activa = activa }, ct);

    public async Task AceptarAsync(Guid id, CancellationToken ct = default)
    {
        await EjecutarAccionAsync(id, "aceptar", ct);
    }

    public async Task RechazarAsync(Guid id, CancellationToken ct = default)
    {
        await EjecutarAccionAsync(id, "rechazar", ct);
    }

    private Task EjecutarAccionAsync(
        Guid id,
        string accion,
        CancellationToken ct) =>
        EnviarAsync(HttpMethod.Post, $"api/vinculaciones/{id}/{accion}", null, ct);

    private async Task EnviarAsync(
        HttpMethod metodo,
        string url,
        object? contenido,
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

        using var solicitud = new HttpRequestMessage(metodo, url)
        {
            Content = contenido is null ? null : JsonContent.Create(contenido)
        };

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
