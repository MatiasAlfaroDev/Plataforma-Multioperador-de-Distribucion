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

    public async Task<IReadOnlyList<EnvioItem>> ListarEnviosAsync(Guid? operadorId, CancellationToken ct = default)
    {
        var url = operadorId is Guid id ? $"api/envios?operadorId={id}" : "api/envios";
        return await http.GetFromJsonAsync<List<EnvioItem>>(url, ct) ?? [];
    }
}
