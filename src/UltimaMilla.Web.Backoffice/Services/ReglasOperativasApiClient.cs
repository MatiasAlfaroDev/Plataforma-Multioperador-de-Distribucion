
using System.Net;
using System.Net.Http.Json;

namespace UltimaMilla.Web.Backoffice.Services;

public sealed class ReglasOperativasApiClient(HttpClient http)
{
    public async Task<ReglasOperativasVigentesItem?> ObtenerVigentesAsync(
        Guid operadorId,
        CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"/api/reglas-operativas/vigente?operadorId={operadorId}",
            ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ReglasOperativasVigentesItem>(
                cancellationToken: ct);
    }

    public async Task ActualizarAsync(
        ActualizarReglasOperativasRequest request,
        CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/reglas-operativas",
            request,
            ct);

        response.EnsureSuccessStatusCode();
    }
}

public sealed record MotivoNoEntregaItem(
    string Codigo,
    string Descripcion,
    bool RequiereDevolucion);

public sealed record ReglasOperativasVigentesItem(
    Guid VersionReglasOperativasId,
    int MaximoIntentosEntrega,
    int HorasMinimasEntreIntentos,
    bool RequierePruebaEntrega,
    int DiasMaximosParaDevolucion,
    DateTimeOffset VigenciaDesde,
    IReadOnlyList<MotivoNoEntregaItem> Motivos,
    bool AdmiteEntregaParcial = false,
    int AccionBultosPendientes = 0,
    bool NuevaVisitaConRecargo = false);

public sealed record ActualizarReglasOperativasRequest(
    Guid OperadorId,
    int MaximoIntentosEntrega,
    int HorasMinimasEntreIntentos,
    bool RequierePruebaEntrega,
    int DiasMaximosParaDevolucion,
    IReadOnlyList<MotivoNoEntregaItem>? Motivos = null,
    // RF 33. La API serializa AccionBultosPendientes como número: 0 = nueva visita, 1 = devolver al comercio.
    bool AdmiteEntregaParcial = false,
    int AccionBultosPendientes = 0,
    bool NuevaVisitaConRecargo = false);