using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>Implementación HTTP de IEnviosApi (mismo estilo que EventsApi de la demo).</summary>
public class EnviosApi : IEnviosApi
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EnviosApi(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<IReadOnlyList<OperadorItem>> ListarOperadoresAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<OperadorItem>>("api/operadores", JsonOptions, ct) ?? [];

    /// <summary>
    /// La ruta del día (RF 18). Si inició sesión un repartidor, son las paradas de SUS hojas de ruta de hoy.
    /// Si es otro perfil (por ejemplo admin.rapi, para probar), la API responde 403 y se descargan
    /// todos los envíos del operador, como antes.
    /// </summary>
    public async Task<IReadOnlyList<EnvioItem>> ListarEnviosAsync(Guid operadorId, CancellationToken ct = default)
    {
        var hojas = await PedirMisHojasAsync(ct);

        // null = no es repartidor (403): se descargan todos los envíos del operador, como antes.
        if (hojas is null)
            return await _http.GetFromJsonAsync<List<EnvioItem>>($"api/envios?operadorId={operadorId}", JsonOptions, ct) ?? [];

        // Cada parada se guarda en el teléfono como un envío de la ruta, en el orden de recorrido.
        return hojas
            .SelectMany(h => h.Paradas.OrderBy(p => p.Orden))
            .Select(p => new EnvioItem(
                p.EnvioId,
                p.ReferenciaExterna,
                operadorId,
                OperadorNombre: string.Empty,
                ComercioNombre: string.Empty,
                Modalidad: string.Empty,
                p.Estado,
                p.Destinatario,
                $"{p.Direccion}, {p.Localidad}",
                p.Bultos.Count,
                PesoTotalKg: 0m,
                FechaAlta: DateTimeOffset.Now))
            .ToList();
    }

    public async Task<IReadOnlyList<HojaDeRutaItem>> ListarMisHojasAsync(CancellationToken ct = default) =>
        await PedirMisHojasAsync(ct)
        ?? throw new RutaNoDisponibleException("La carga del vehículo es para repartidores: iniciá sesión con un usuario repartidor.");

    /// <summary>GET /api/repartidor/hojas-de-ruta. Devuelve null si el usuario no es repartidor (403).</summary>
    private async Task<List<HojaDeRutaItem>?> PedirMisHojasAsync(CancellationToken ct)
    {
        // "Hoy" según el teléfono (hora de Uruguay), no según el servidor (UTC).
        var hoy = DateTime.Now.ToString("yyyy-MM-dd");
        using var respuesta = await _http.GetAsync($"api/repartidor/hojas-de-ruta?fecha={hoy}", ct);

        if (respuesta.StatusCode == HttpStatusCode.Forbidden)
            return null;

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            throw new RutaNoDisponibleException(await LeerMotivoAsync(respuesta, ct));

        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<List<HojaDeRutaItem>>(JsonOptions, ct) ?? [];
    }

    /// <summary>POST /api/repartidor/hojas-de-ruta/{id}/despachar con los códigos escaneados (RF 17 y 19).</summary>
    public async Task<(bool Ok, string Mensaje)> DespacharAsync(
        Guid hojaId, IReadOnlyList<string> bultosEscaneados, CancellationToken ct = default)
    {
        using var respuesta = await _http.PostAsJsonAsync(
            $"api/repartidor/hojas-de-ruta/{hojaId}/despachar",
            new { bultosEscaneados },
            ct);

        if (respuesta.IsSuccessStatusCode)
        {
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaDespacho>(JsonOptions, ct);
            return (true, $"Ruta despachada: {cuerpo?.EnviosEnTransito ?? 0} envíos en tránsito.");
        }

        // 400 = la carga no coincide (el servidor dice qué falta o sobra) o la hoja no se puede despachar.
        return (false, await LeerMotivoAsync(respuesta, ct));
    }

    private sealed record RespuestaDespacho(Guid HojaDeRutaId, int EnviosEnTransito);

    /// <summary>
    /// POST /api/envios/{envioId}/eventos-movil. El contrato está en LEEME-OFFLINE.md
    /// para quien implemente el endpoint en la API.
    /// </summary>
    public async Task<(ResultadoEnvio Resultado, string? Detalle)> EnviarEventoAsync(EventoPendiente evento, CancellationToken ct = default)
    {
        var esEntrega = evento.Tipo == "Entregado";
        var cuerpo = new
        {
            idEvento = evento.Id,
            tipo = evento.Tipo,
            motivo = evento.Motivo,
            receptor = evento.Receptor,
            fechaHora = evento.FechaHora,
            latitud = evento.Latitud,
            longitud = evento.Longitud,
            // La misma foto es prueba de entrega o evidencia del intento fallido, según el tipo.
            referenciaFotografia = esEntrega ? evento.ReferenciaFoto : null,
            evidenciaFallo = esEntrega ? null : evento.ReferenciaFoto
        };

        try
        {
            using var respuesta = await _http.PostAsJsonAsync($"api/envios/{evento.EnvioId}/eventos-movil", cuerpo, ct);

            // 2xx = guardado. 409 = el servidor ya tenía ese idEvento: también es éxito (idempotencia).
            if (respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.Conflict)
                return (ResultadoEnvio.Aceptado, null);

            return (ResultadoEnvio.Rechazado, await LeerMotivoAsync(respuesta, ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (ResultadoEnvio.SinConexion, ex.Message);
        }
    }

    /// <summary>
    /// La API explica el rechazo en un ProblemDetails: { "title": "El envío debe estar en tránsito..." }.
    /// Se guarda solo ese texto, que es el que ve el repartidor.
    /// </summary>
    private static async Task<string> LeerMotivoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            return "Sesión vencida: iniciá sesión de nuevo.";

        var texto = await respuesta.Content.ReadAsStringAsync(ct);
        try
        {
            using var json = JsonDocument.Parse(texto);
            if (json.RootElement.TryGetProperty("title", out var titulo) && titulo.GetString() is { Length: > 0 } motivo)
                return motivo;
        }
        catch (JsonException)
        {
            // No era JSON: se guarda el texto tal cual.
        }
        return $"Error {(int)respuesta.StatusCode}: {Recortar(texto)}";
    }
    private static string Recortar(string texto) => texto.Length <= 200 ? texto : texto[..200];
}

/// <summary>El servidor respondió, pero no hay ruta para este usuario (por ejemplo, no está vinculado a un repartidor).</summary>
public sealed class RutaNoDisponibleException(string mensaje) : Exception(mensaje);
