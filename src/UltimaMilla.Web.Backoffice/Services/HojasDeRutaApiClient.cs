
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record OperadorRutaApiDto(
    Guid Id,
    string Nombre);

public sealed record RepartidorRutaApiDto(
    Guid Id,
    string Nombre);

public sealed record VehiculoRutaApiDto(
    Guid Id,
    string Matricula,
    decimal CapacidadPesoKg,
    decimal CapacidadVolumenM3);

public sealed record EnvioRutaApiDto(
    Guid Id,
    string ReferenciaExterna,
    string Localidad,
    string Estado,
    decimal PesoKg,
    decimal VolumenM3,
    DateOnly? FechaComprometida,
    TimeOnly? HoraDesde,
    TimeOnly? HoraHasta,
    bool TieneCompromiso);

public sealed record OpcionesHojaRutaApiDto(
    List<RepartidorRutaApiDto> Repartidores,
    List<VehiculoRutaApiDto> Vehiculos,
    List<EnvioRutaApiDto> Envios,
    List<FranjaRutaApiDto> Franjas);

public sealed record HojaRutaApiDto(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    Guid RepartidorId,
    Guid VehiculoId,
    int CantidadParadas,
    decimal PesoAsignadoKg,
    bool Despachada);

public sealed record FranjaRutaApiDto(
    TimeOnly HoraDesde,
    TimeOnly HoraHasta);

public sealed record CrearHojaRutaApiRequest(
    Guid OperadorId,
    Guid RepartidorId,
    Guid VehiculoId,
    DateOnly Fecha,
    TimeOnly HoraSalida,
    TimeOnly HoraEntregaDesde,
    TimeOnly HoraEntregaHasta,
    int MaximoParadas,
    IReadOnlyList<Guid> EnviosIds,
    IReadOnlyList<Guid> RetirosIds,
    IReadOnlyList<Guid>? DevolucionesIds = null,
    IReadOnlyList<Guid>? EntregasRetirosIds = null);

public sealed class HojasDeRutaApiClient(HttpClient http)
{
    public async Task<List<OperadorRutaApiDto>> ListarOperadoresAsync(
        CancellationToken ct)
    {
        return await http.GetFromJsonAsync<List<OperadorRutaApiDto>>(
            "api/operadores", ct) ?? [];
    }

    public async Task<OpcionesHojaRutaApiDto> ObtenerOpcionesAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await http.GetFromJsonAsync<OpcionesHojaRutaApiDto>(
            $"api/hojas-de-ruta/opciones?operadorId={operadorId}",
            ct)
            ?? throw new InvalidOperationException(
                "La API no devolvió las opciones de planificación.");
    }

    public async Task<List<HojaRutaApiDto>> ListarAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        return await http.GetFromJsonAsync<List<HojaRutaApiDto>>(
            $"api/hojas-de-ruta?operadorId={operadorId}",
            ct) ?? [];
    }

    public async Task<int> DespacharAsync(
        Guid operadorId,
        Guid hojaId,
        CancellationToken ct)
    {
        using var solicitud = await CrearSolicitudProtegidaAsync(
            HttpMethod.Post,
            $"api/hojas-de-ruta/{hojaId}/despachar?operadorId={operadorId}",
            null,
            ct);

        using var respuesta = await http.SendAsync(solicitud, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var contenido = await respuesta.Content.ReadAsStringAsync(ct);

            throw new InvalidOperationException(
                $"No se pudo despachar la hoja de ruta. " +
                $"HTTP {(int)respuesta.StatusCode}: {contenido}");
        }

        var resultado = await respuesta.Content
            .ReadFromJsonAsync<DespachoRespuesta>(
                cancellationToken: ct);

        return resultado?.EnviosActualizados
            ?? throw new InvalidOperationException(
                "La API no devolvió la cantidad de envíos despachados.");
    }

    public async Task<Guid> CrearAsync(
        CrearHojaRutaApiRequest solicitud,
        CancellationToken ct)
    {
        using var solicitudHttp = await CrearSolicitudProtegidaAsync(
            HttpMethod.Post,
            "api/hojas-de-ruta",
            JsonContent.Create(solicitud),
            ct);

        using var respuesta = await http.SendAsync(solicitudHttp, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var contenido = await respuesta.Content.ReadAsStringAsync(ct);

            throw new InvalidOperationException(
                $"No se pudo crear la hoja de ruta. " +
                $"HTTP {(int)respuesta.StatusCode}: {contenido}");
        }

        var resultado = await respuesta.Content
            .ReadFromJsonAsync<HojaCreadaRespuesta>(
                cancellationToken: ct);

        return resultado?.Id
            ?? throw new InvalidOperationException(
                "La API no devolvió el identificador de la hoja.");
    }

    private async Task<HttpRequestMessage> CrearSolicitudProtegidaAsync(
        HttpMethod metodo,
        string ruta,
        HttpContent? contenido,
        CancellationToken ct)
    {
        using var respuestaToken = await http.GetAsync(
            "api/antiforgery/token",
            ct);

        respuestaToken.EnsureSuccessStatusCode();

        var datosToken = await respuestaToken.Content
            .ReadFromJsonAsync<TokenAntiforgeryRespuesta>(
                cancellationToken: ct);

        if (string.IsNullOrWhiteSpace(datosToken?.Token))
        {
            throw new InvalidOperationException(
                "No se pudo obtener el token de seguridad.");
        }

        var solicitud = new HttpRequestMessage(metodo, ruta)
        {
            Content = contenido
        };

        solicitud.Headers.TryAddWithoutValidation(
            "X-CSRF-TOKEN",
            datosToken.Token);

        if (respuestaToken.Headers.TryGetValues(
            "Set-Cookie",
            out var cookies))
        {
            var cookieAntiforgery = cookies
                .Select(c => c.Split(';')[0])
                .FirstOrDefault(c =>
                    c.StartsWith(
                        ".AspNetCore.Antiforgery.",
                        StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(cookieAntiforgery))
            {
                solicitud.Options.Set(
                    CookieAutenticacionHandler.CookieAntiforgeryKey,
                    cookieAntiforgery);
            }
        }

        return solicitud;
    }

    private sealed record TokenAntiforgeryRespuesta(string Token);

    private sealed record DespachoRespuesta(
        Guid HojaDeRutaId,
        int EnviosActualizados);

    private sealed record HojaCreadaRespuesta(Guid Id);
}
