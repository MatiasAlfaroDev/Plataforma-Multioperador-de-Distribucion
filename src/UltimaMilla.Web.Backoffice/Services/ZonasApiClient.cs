
using System.Net.Http.Json;
using System.Text.Json;

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
            var mensaje = await LeerErrorAsync(respuesta, ct);
            throw new InvalidOperationException(mensaje);
        }

        var resultado = await respuesta.Content
            .ReadFromJsonAsync<ZonaCreadaResponse>(
                cancellationToken: ct);

        return resultado?.Id
            ?? throw new InvalidOperationException(
                "La API no devolvió el identificador de la zona.");
    }

    private static async Task<string> LeerErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken ct)
    {
        try
        {
            using var documento = JsonDocument.Parse(
                await respuesta.Content.ReadAsStringAsync(ct));

            var raiz = documento.RootElement;

            if (raiz.TryGetProperty("errors", out var errores) &&
                errores.ValueKind == JsonValueKind.Object)
            {
                var mensajes = errores.EnumerateObject()
                    .SelectMany(x =>
                        x.Value.ValueKind == JsonValueKind.Array
                            ? x.Value.EnumerateArray()
                                .Select(y => y.ToString())
                            : [x.Value.ToString()])
                    .ToArray();

                if (mensajes.Length > 0)
                    return string.Join(" ", mensajes);
            }

            if (raiz.TryGetProperty("detail", out var detalle) &&
                !string.IsNullOrWhiteSpace(detalle.ToString()))
                return detalle.ToString();

            if (raiz.TryGetProperty("title", out var titulo))
                return titulo.ToString();
        }
        catch (JsonException)
        {
            // Respuesta sin formato ProblemDetails.
        }

        return $"No se pudo guardar la zona. HTTP {(int)respuesta.StatusCode}.";
    }
}
