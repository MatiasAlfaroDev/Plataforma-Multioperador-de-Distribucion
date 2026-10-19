using System.Net;
using System.Text.Json;

namespace UltimaMilla.Web.Portal.Services;

public sealed record CuentaComercialItem(Guid Id, Guid OperadorId, string OperadorNombre, Guid ComercioId, string ComercioNombre, bool Activa);

/// <summary>Copia de lo que devuelve GET /api/zonas.</summary>
public sealed record ZonaItem(Guid Id, string Nombre, string Departamento);

/// <summary>Resultado de una operación de alta de envío.</summary>
public sealed record ResultadoAlta(bool Exito, string Mensaje);

/// <summary>Cliente HTTP tipado hacia la API. El Portal nunca toca la base de datos.</summary>
public sealed class EnviosApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<CuentaComercialItem>> ListarCuentasAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CuentaComercialItem>>("api/cuentas-comerciales", ct) ?? [];

    /// <summary>Zonas de cobertura del operador: el departamento del envío tiene que ser uno de ellos para que haya tarifa.</summary>
    public async Task<IReadOnlyList<ZonaItem>> ListarZonasAsync(Guid operadorId, CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<ZonaItem>>($"api/zonas?operadorId={operadorId}", ct) ?? [];
    }

    public async Task<ResultadoAlta> CrearAsync(NuevoEnvioModel m, CancellationToken ct = default)
    {
        // Mismos nombres que CrearEnvioCommand de Application.
        var cuerpo = new
        {
            cuentaComercialId = Guid.Parse(m.CuentaComercialId),
            referenciaExterna = m.ReferenciaExterna,
            modalidad = m.Modalidad,
            destinatarioNombre = m.DestinatarioNombre,
            destinatarioTelefono = m.DestinatarioTelefono,
            calle = m.Calle,
            numero = m.Numero,
            localidad = m.Localidad,
            departamento = m.Departamento,
            bultos = m.Bultos.Select(b => new
            {
                pesoKg = b.PesoKg,
                altoCm = b.AltoCm,
                anchoCm = b.AnchoCm,
                profundidadCm = b.ProfundidadCm
            }).ToArray()
        };

        try
        {
            var respuesta = await http.PostAsJsonAsync("api/envios", cuerpo, ct);
            if (respuesta.StatusCode == HttpStatusCode.Created)
                return new(true, $"Envío {m.ReferenciaExterna} dado de alta.");

            return new(false, await LeerProblemaAsync(respuesta, ct));
        }
        catch (HttpRequestException)
        {
            return new(false, "No se pudo dar de alta el envío. Probá de nuevo en unos minutos.");
        }
    }

    /// <summary>Arma un mensaje legible a partir del ProblemDetails que devuelve la API.</summary>
    private static async Task<string> LeerProblemaAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
            var raiz = doc.RootElement;

            if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
            {
                var mensajes = errores.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString()))
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                return string.Join(" ", mensajes);
            }

            if (raiz.TryGetProperty("title", out var titulo))
                return titulo.GetString() ?? "La API rechazó el pedido.";
        }
        catch (JsonException)
        {
            // La respuesta no era JSON: se usa el mensaje genérico.
        }

        return $"La API respondió {(int)respuesta.StatusCode}.";
    }
}
