using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

/// <summary>Avisos a comercios del operador, con reintento de los fallidos (RNF 6.8 y 6.9).</summary>
public sealed class AvisosModel(AvisosApiClient api) : PageModel
{
    /// <summary>Por defecto los fallidos, que es lo que el operador tiene que mirar; vacío = todos.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; } = "Fallida";

    public IReadOnlyList<AvisoItem> Avisos { get; private set; } = [];

    public string? Error { get; private set; }

    [TempData]
    public string? Mensaje { get; set; }

    [TempData]
    public bool Reencolado { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarAsync(ct);
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        try
        {
            Avisos = await api.ListarAsync(Estado, ct);
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode is not null
                ? $"No se pudieron cargar los avisos (HTTP {(int)ex.StatusCode})."
                : "No se pudo conectar con la API.";
        }
        catch (UnauthorizedAccessException)
        {
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
        }
    }

    public async Task<IActionResult> OnPostReenviarAsync(
        Guid id,
        CancellationToken ct)
    {
        try
        {
            await api.ReenviarAsync(id, ct);
            Reencolado = true;
            Mensaje = "Aviso reencolado: el Worker lo vuelve a enviar en unos segundos.";
        }
        catch (HttpRequestException ex)
        {
            Mensaje = ex.Message;
        }

        return RedirectToPage(new { Estado });
    }
}
