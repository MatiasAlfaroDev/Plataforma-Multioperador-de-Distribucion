namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>Copia de lo que devuelve GET /api/mensajes-fallidos.</summary>
public sealed record MensajeFallidoItem(string? TipoMensaje, string? Error, string Cuerpo);

public sealed record ColaFallidosItem(string Cola, int Cantidad, IReadOnlyList<MensajeFallidoItem> Muestra);

public sealed class MensajesFallidosApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ColaFallidosItem>> ListarAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ColaFallidosItem>>("api/mensajes-fallidos", ct) ?? [];

    /// <summary>Devuelve los mensajes de la cola a su cola original; devuelve cuántos movió.</summary>
    public async Task<int> ReintentarAsync(string cola, CancellationToken ct = default)
    {
        var respuesta = await http.PostAsync($"api/mensajes-fallidos/{Uri.EscapeDataString(cola)}/reintentar", null, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                await ErrorDeApi.LeerAsync(respuesta, "La API rechazó el reintento", ct),
                null,
                respuesta.StatusCode);
        }

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, int>>(ct);
        return cuerpo?.GetValueOrDefault("reintentados") ?? 0;
    }
}
