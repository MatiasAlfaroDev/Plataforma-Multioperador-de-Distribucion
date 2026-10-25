using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class IndexModel(EnviosApiClient api) : PageModel
{
    public IReadOnlyList<EnvioItem> Envios { get; private set; } = [];

    public string? Error { get; private set; }

    public string OperadorNombre =>
        Envios.FirstOrDefault()?.OperadorNombre
        ?? "Operador seleccionado";

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarDatosAsync(ct);
    }

    private async Task CargarDatosAsync(CancellationToken ct)
    {
        try
        {
            Envios = await api.ListarEnviosAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode is not null
                ? $"No se pudieron cargar los envíos (HTTP {(int)ex.StatusCode})."
                : "No se pudo conectar con la API.";
        }
        catch (UnauthorizedAccessException)
        {
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
        }
    }

    public async Task<IActionResult> OnPostRecibirAsync(
        Guid envioId,
        string identificaciones,
        string responsable,
        CancellationToken ct)
    {
        var codigos = (identificaciones ?? string.Empty)
            .Split(
                [',', ';', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        try
        {
            await api.RecibirEnDepositoAsync(
                envioId,
                codigos,
                responsable ?? string.Empty,
                ct);

            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            await CargarDatosAsync(ct);

            Error = ex.StatusCode is not null
                ? $"Error de recepción (HTTP {(int)ex.StatusCode}): {ex.Message}"
                : $"Error de conexión: {ex.Message}";

            return Page();
        }
    }
}