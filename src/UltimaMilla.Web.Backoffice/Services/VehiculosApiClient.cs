using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record VehiculoAdminDto(
    Guid Id, string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3, bool Activo);

public sealed record AltaVehiculo(string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3);

public sealed class VehiculosApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<VehiculoAdminDto>> ListarAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<VehiculoAdminDto>>("api/vehiculos", ct) ?? [];

    public async Task AltaAsync(AltaVehiculo alta, CancellationToken ct = default)
    {
        using var solicitud = await CrearSolicitudAsync(HttpMethod.Post, "api/vehiculos", alta, ct);
        await EnviarAsync(solicitud, ct);
    }

    public async Task CambiarEstadoAsync(Guid id, bool activo, CancellationToken ct = default)
    {
        using var solicitud = await CrearSolicitudAsync(
            HttpMethod.Post, $"api/vehiculos/{id}/activo", new { Activo = activo }, ct);
        await EnviarAsync(solicitud, ct);
    }

    private async Task<HttpRequestMessage> CrearSolicitudAsync(
        HttpMethod metodo, string url, object contenido, CancellationToken ct)
    {
        using var respuestaToken = await http.GetAsync("api/antiforgery/token", ct);
        respuestaToken.EnsureSuccessStatusCode();
        var token = await respuestaToken.Content.ReadFromJsonAsync<TokenAntiforgery>(cancellationToken: ct);

        var solicitud = new HttpRequestMessage(metodo, url) { Content = JsonContent.Create(contenido) };
        solicitud.Headers.Add("X-CSRF-TOKEN", token?.Token);
        return solicitud;
    }

    private async Task EnviarAsync(HttpRequestMessage solicitud, CancellationToken ct)
    {
        using var respuesta = await http.SendAsync(solicitud, ct);
        if (!respuesta.IsSuccessStatusCode)
            throw new HttpRequestException(await ErrorDeApi.LeerAsync(respuesta, "No se pudo guardar el vehículo", ct), null, respuesta.StatusCode);
    }

    private sealed record TokenAntiforgery(string Token);
}
