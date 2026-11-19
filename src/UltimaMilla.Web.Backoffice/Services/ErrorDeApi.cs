using System.Text.Json;

namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>
/// Saca el texto del error que devuelve la API (ProblemDetails, <c>{ mensaje }</c> o <c>{ errores }</c>) para
/// que el toast muestre solo eso y nunca el JSON.
/// </summary>
public static class ErrorDeApi
{
    public static async Task<string> LeerAsync(
        HttpResponseMessage respuesta,
        string mensajePorDefecto,
        CancellationToken ct) =>
        Extraer(await respuesta.Content.ReadAsStringAsync(ct))
            ?? $"{mensajePorDefecto} (HTTP {(int)respuesta.StatusCode}).";

    /// <summary>Red de seguridad del toast: si el texto trae pegado el JSON de la API, deja solo su mensaje.</summary>
    public static string Limpiar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return texto ?? "";

        var inicio = texto.IndexOf('{');
        return inicio >= 0 && Extraer(texto[inicio..]) is { } mensaje ? mensaje : texto;
    }

    private static string? Extraer(string cuerpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            var raiz = documento.RootElement;

            if (raiz.ValueKind == JsonValueKind.String)
                return Texto(raiz);

            if (raiz.ValueKind != JsonValueKind.Object)
                return null;

            // Validaciones de FluentValidation: { errors: { campo: [mensajes] } }.
            if (Propiedad(raiz, "errors") is { ValueKind: JsonValueKind.Object } errores)
            {
                var mensajes = errores.EnumerateObject()
                    .SelectMany(x => x.Value.ValueKind == JsonValueKind.Array
                        ? x.Value.EnumerateArray().Select(y => y.ToString())
                        : [x.Value.ToString()])
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToArray();

                if (mensajes.Length > 0)
                    return string.Join(" ", mensajes);
            }

            // Endpoints que responden { errores: [..] } o { mensaje }.
            if (Propiedad(raiz, "errores") is { ValueKind: JsonValueKind.Array } lista &&
                lista.GetArrayLength() > 0)
                return string.Join(" ", lista.EnumerateArray().Select(x => x.ToString()));

            return Texto(Propiedad(raiz, "detail"))
                ?? Texto(Propiedad(raiz, "mensaje"))
                ?? Texto(Propiedad(raiz, "title"));
        }
        catch (JsonException)
        {
            // No es JSON: el que llama usa su mensaje por defecto.
            return null;
        }
    }

    private static JsonElement? Propiedad(JsonElement objeto, string nombre) =>
        objeto.EnumerateObject()
            .Where(p => string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase))
            .Select(p => (JsonElement?)p.Value)
            .FirstOrDefault();

    private static string? Texto(JsonElement? valor) =>
        valor is { ValueKind: JsonValueKind.String } v && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}
