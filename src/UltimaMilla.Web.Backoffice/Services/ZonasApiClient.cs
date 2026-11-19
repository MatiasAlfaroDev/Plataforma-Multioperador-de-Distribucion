
using System.Net;
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed record ZonaListadoItem(
    Guid Id,
    string Nombre,
    string Departamento);

public sealed record CoberturaZonaRequest(
    string Localidad,
    string? Sector);

public sealed record FranjaZonaRequest(
    TimeOnly HoraDesde,
    TimeOnly HoraHasta);

public sealed record CrearZonaRequest(
    Guid OperadorId,
    string Nombre,
    string Departamento,
    int MinutosTraslado,
    IReadOnlyList<CoberturaZonaRequest> Coberturas,
    IReadOnlyList<FranjaZonaRequest> Franjas);

public sealed record ZonaCreadaResponse(Guid Id);

/// <summary>Lo que devuelve GET /api/zonas/{id}: la zona completa, para editarla.</summary>
public sealed record ZonaDetalle(
    Guid Id,
    string Nombre,
    string Departamento,
    int? MinutosTraslado,
    IReadOnlyList<CoberturaZonaRequest> Coberturas,
    IReadOnlyList<FranjaZonaRequest> Franjas);

/// <summary>Lo que se manda a PUT /api/zonas/{id}.</summary>
public sealed record EditarZonaRequest(
    Guid OperadorId,
    string Nombre,
    string Departamento,
    int MinutosTraslado,
    IReadOnlyList<CoberturaZonaRequest> Coberturas,
    IReadOnlyList<FranjaZonaRequest> Franjas);

public sealed class ZonasApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ZonaListadoItem>> ListarAsync(
        Guid operadorId,
        CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<ZonaListadoItem>>(
            $"api/zonas?operadorId={operadorId}",
            ct) ?? [];
    }

    public async Task<Guid> CrearAsync(
        CrearZonaRequest solicitud,
        CancellationToken ct = default)
    {
        using var respuesta = await http.PostAsJsonAsync(
            "api/zonas",
            solicitud,
            ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var mensaje = await ErrorDeApi.LeerAsync(respuesta, "No se pudo guardar la zona", ct);
            throw new InvalidOperationException(mensaje);
        }

        var resultado = await respuesta.Content
            .ReadFromJsonAsync<ZonaCreadaResponse>(
                cancellationToken: ct);

        return resultado?.Id
            ?? throw new InvalidOperationException(
                "La API no devolvió el identificador de la zona.");
    }

    /// <summary>La zona completa, o null si no existe (o es de otro operador).</summary>
    public async Task<ZonaDetalle?> ObtenerAsync(
        Guid operadorId,
        Guid zonaId,
        CancellationToken ct = default)
    {
        using var respuesta = await http.GetAsync(
            $"api/zonas/{zonaId}?operadorId={operadorId}",
            ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException(await ErrorDeApi.LeerAsync(respuesta, "No se pudo guardar la zona", ct));

        return await respuesta.Content.ReadFromJsonAsync<ZonaDetalle>(cancellationToken: ct);
    }

    public async Task EditarAsync(
        Guid zonaId,
        EditarZonaRequest solicitud,
        CancellationToken ct = default)
    {
        using var respuesta = await http.PutAsJsonAsync(
            $"api/zonas/{zonaId}",
            solicitud,
            ct);

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException(await ErrorDeApi.LeerAsync(respuesta, "No se pudo guardar la zona", ct));
    }

}
