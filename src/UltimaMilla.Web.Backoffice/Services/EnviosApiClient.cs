namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>Copia de los datos que devuelve GET /api/operadores.</summary>
public sealed record OperadorItem(Guid Id, string Nombre, string ColorPrimario);

/// <summary>Copia de los datos que devuelve GET /api/envios.</summary>
public sealed record EnvioItem(
    Guid Id,
    string ReferenciaExterna,
    Guid OperadorId,
    string OperadorNombre,
    string ComercioNombre,
    string Modalidad,
    string Estado,
    string DestinatarioNombre,
    string Direccion,
    int CantidadBultos,
    decimal PesoTotalKg,
    decimal? TarifaCalculada,
    DateTimeOffset FechaAlta);

public sealed class EnviosApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<OperadorItem>> ListarOperadoresAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<OperadorItem>>("api/operadores", ct) ?? [];

   public async Task<IReadOnlyList<EnvioItem>> ListarEnviosAsync(
    CancellationToken ct = default)
{
    return await http.GetFromJsonAsync<List<EnvioItem>>(
        "api/envios",
        ct) ?? [];
}

    public async Task RecibirEnDepositoAsync(
        Guid envioId,
        IReadOnlyList<string> identificaciones,
        string responsable,
        CancellationToken ct = default)
    {
        var respuesta = await http.PostAsJsonAsync(
            $"api/envios/{envioId}/recepcion",
            new
            {
                IdentificacionesEscaneadas = identificaciones,
                Responsable = responsable
            },
            ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync(ct);

            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(detalle)
                    ? $"La API rechazó la recepción (HTTP {(int)respuesta.StatusCode})."
                    : $"La API respondió: {detalle}",
                null,
                respuesta.StatusCode);
        }
            }
}
