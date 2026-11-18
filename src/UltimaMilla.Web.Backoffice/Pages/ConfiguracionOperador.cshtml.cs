
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class ConfiguracionOperadorModel(
    ConfiguracionOperadorApiClient api) : PageModel
{
    public ConfiguracionOperadorDto? Operador { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "Ingresá un color.")]
    [RegularExpression(
        @"^#[0-9A-Fa-f]{6}$",
        ErrorMessage = "El color debe tener formato #RRGGBB.")]
    public string ColorPrimario { get; set; } = "#1D56B3";

    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarOperadorAsync(ct);

        if (Operador is not null)
            ColorPrimario = Operador.ColorPrimario;
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await CargarOperadorAsync(ct);
            return Page();
        }

        try
        {
            await api.ActualizarColorAsync(ColorPrimario, ct);

            TempData["MensajeExito"] =
                "El color del operador se actualizó correctamente.";

            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode is not null
                ? $"No se pudo actualizar el color (HTTP {(int)ex.StatusCode})."
                : "No se pudo conectar con la API.";

            await CargarOperadorAsync(ct);
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
            return Page();
        }
    }

    private async Task CargarOperadorAsync(
        CancellationToken ct)
    {
        try
        {
            Operador = await api.ObtenerAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode is not null
                ? $"No se pudo cargar el operador (HTTP {(int)ex.StatusCode})."
                : "No se pudo conectar con la API.";
        }
        catch (UnauthorizedAccessException)
        {
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
        }
    }
}
