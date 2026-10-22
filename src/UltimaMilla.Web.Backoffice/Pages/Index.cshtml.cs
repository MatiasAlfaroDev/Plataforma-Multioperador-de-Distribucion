
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class IndexModel(EnviosApiClient api) : PageModel
{
    /// <summary>
    /// Provisorio hasta implementar la autenticación:
    /// el operador se selecciona desde el Backoffice.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public Guid? OperadorId { get; set; }

    public IReadOnlyList<OperadorItem> Operadores { get; private set; } = [];

    public IReadOnlyList<EnvioItem> Envios { get; private set; } = [];

    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarDatosAsync(ct);
    }

    private async Task CargarDatosAsync(CancellationToken ct)
    {
        try
        {
            Operadores = await api.ListarOperadoresAsync(ct);

            Envios = await api.ListarEnviosAsync(
                OperadorId,
                ct);
        }
        catch (HttpRequestException)
        {
            Error =
                "No se pudieron cargar los envíos. " +
                "Probá de nuevo en unos minutos.";
        }
    }

    public string ColorDe(Guid operadorId)
    {
        return Operadores
            .FirstOrDefault(o => o.Id == operadorId)?
            .ColorPrimario ?? "#999";
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

            return RedirectToPage(
                new { operadorId = OperadorId });
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
