using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

/// <summary>Colas de mensajes fallidos del broker para el administrador de plataforma (RNF 6.8): consulta y reintento.</summary>
public sealed class MensajesFallidosModel(MensajesFallidosApiClient api) : PageModel
{
    public IReadOnlyList<ColaFallidosItem> Colas { get; private set; } = [];

    public string? Error { get; private set; }

    [TempData]
    public string? Mensaje { get; set; }

    [TempData]
    public bool Reintentado { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarAsync(ct);
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        try
        {
            Colas = await api.ListarAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode is not null
                ? $"No se pudieron cargar las colas (HTTP {(int)ex.StatusCode})."
                : "No se pudo conectar con la API.";
        }
        catch (UnauthorizedAccessException)
        {
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
        }
    }

    public async Task<IActionResult> OnPostReintentarAsync(string cola, CancellationToken ct)
    {
        try
        {
            var n = await api.ReintentarAsync(cola, ct);
            Reintentado = true;
            Mensaje = $"{n} mensajes devueltos a su cola: el Worker los procesa en unos segundos.";
        }
        catch (HttpRequestException ex)
        {
            Mensaje = ex.Message;
        }

        return RedirectToPage();
    }
}
