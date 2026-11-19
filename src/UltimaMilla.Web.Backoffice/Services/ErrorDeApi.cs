using System.Text.Json;

namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>Saca el mensaje de un ProblemDetails de la API para mostrarlo tal cual en el toast.</summary>
public static class ErrorDeApi
{
    public static async Task<string> LeerAsync(
        HttpResponseMessage respuesta,
        string mensajePorDefecto,
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

        return $"{mensajePorDefecto} (HTTP {(int)respuesta.StatusCode}).";
    }
}
