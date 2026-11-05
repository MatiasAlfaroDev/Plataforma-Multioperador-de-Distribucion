using System.Net.Http.Json;
using System.Text.Json.Nodes;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Mensajeria;

/// <summary>
/// Colas de mensajes fallidos del broker sobre la API de administración de RabbitMQ (RNF 6.8: el administrador
/// las consulta y las reintenta). MassTransit deja en la cola &lt;cola&gt;_error
/// los mensajes que agotaron sus reintentos, con el motivo en la cabecera MT-Fault-Message;
/// reintentar es devolverlos a &lt;cola&gt;, y es seguro porque los consumidores son idempotentes.
/// </summary>
public sealed class ColaDeFallidos(HttpClient http) : IColaDeFallidos
{
    private const string Vhost = "%2F";

    /// <summary>
    /// Las colas *_error con mensajes, cada una con hasta <paramref name="muestra"/> mensajes sin sacarlos de la cola.
    /// </summary>
    public async Task<IReadOnlyList<ColaFallidosDto>> ListarAsync(int muestra, CancellationToken ct)
    {
        var respuesta = await http.GetAsync($"api/queues/{Vhost}?columns=name,messages", ct);
        respuesta.EnsureSuccessStatusCode();

        var colas = await respuesta.Content.ReadFromJsonAsync<JsonArray>(ct) ?? [];
        var resultado = new List<ColaFallidosDto>();

        foreach (var cola in colas)
        {
            var nombre = cola["name"]?.ToString();
            var mensajes = cola["messages"]?.GetValue<int>() ?? 0;

            if (nombre is not null && nombre.EndsWith("_error") && mensajes > 0)
            {
                var muestraMensajes = await PedirAsync(nombre, muestra, "ack_requeue_true", ct);
                var mensajesDto = muestraMensajes.Select(Mapear).ToList();

                // Las estadísticas del broker se refrescan cada ~5 s: si el get no trae nada, la cola ya está vacía.
                if (mensajesDto.Count > 0)
                    resultado.Add(new ColaFallidosDto(nombre, Math.Max(mensajes, mensajesDto.Count), mensajesDto));
            }
        }

        return resultado.OrderBy(c => c.Cola).ToList();
    }

    /// <summary>
    /// Devuelve todos los mensajes de la cola de fallidos a su cola original; devuelve cuántos movió.
    /// </summary>
    public async Task<int> ReintentarAsync(string colaDeFallidos, CancellationToken ct)
    {
        var colaOriginal = colaDeFallidos[..^"_error".Length];
        var contado = 0;

        for (var i = 0; i < 1000; i++)
        {
            var lote = await PedirAsync(colaDeFallidos, 1, "ack_requeue_false", ct);
            if (lote.Count == 0)
                break;

            // De a uno: si algo falla a mitad de camino queda un solo mensaje en juego, y ese vuelve a la cola de fallidos.
            var mensaje = lote[0];
            bool movido;
            try
            {
                movido = await PublicarAsync(colaOriginal, mensaje, ct);
            }
            catch (HttpRequestException)
            {
                movido = false;
            }

            if (!movido)
            {
                await PublicarAsync(colaDeFallidos, mensaje, ct);
                throw new InvalidOperationException($"No se pudo devolver el mensaje a {colaOriginal}.");
            }

            contado++;
        }

        return contado;
    }

    private async Task<JsonArray> PedirAsync(string cola, int cantidad, string ackmode, CancellationToken ct)
    {
        var cuerpo = new
        {
            count = cantidad,
            ackmode,
            encoding = "auto",
            truncate = 50000
        };

        using var respuesta = await http.PostAsJsonAsync(
            $"api/queues/{Vhost}/{Uri.EscapeDataString(cola)}/get",
            cuerpo,
            ct);

        respuesta.EnsureSuccessStatusCode();

        var json = await respuesta.Content.ReadFromJsonAsync<JsonNode>(ct);
        return json as JsonArray ?? [];
    }

    private async Task<bool> PublicarAsync(string cola, JsonNode mensaje, CancellationToken ct)
    {
        // Clones: un JsonNode no puede tener dos padres.
        var cuerpo = new JsonObject
        {
            ["properties"] = mensaje["properties"]?.DeepClone(),
            ["routing_key"] = cola,
            ["payload"] = mensaje["payload"]?.DeepClone(),
            ["payload_encoding"] = mensaje["payload_encoding"]?.DeepClone()
        };

        using var respuesta = await http.PostAsJsonAsync(
            $"api/exchanges/{Vhost}/amq.default/publish",
            cuerpo,
            ct);

        respuesta.EnsureSuccessStatusCode();

        var jsonRespuesta = await respuesta.Content.ReadFromJsonAsync<JsonNode>(ct);
        return jsonRespuesta?["routed"]?.GetValue<bool>() == true;
    }

    private static MensajeFallidoDto Mapear(JsonNode m)
    {
        var headers = m["properties"]?["headers"];
        var error = headers?["MT-Fault-Message"]?.ToString();
        var cuerpo = m["payload"]?.ToString() ?? "";
        var tipoMensaje = ObtenerTipoMensaje(cuerpo);

        return new MensajeFallidoDto(tipoMensaje, error, cuerpo);
    }

    private static string? ObtenerTipoMensaje(string cuerpo)
    {
        try
        {
            var json = JsonNode.Parse(cuerpo);
            var tipo = json?["messageType"]?[0]?.ToString();
            if (tipo?.StartsWith("urn:message:") == true)
                tipo = tipo["urn:message:".Length..];
            return tipo;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
