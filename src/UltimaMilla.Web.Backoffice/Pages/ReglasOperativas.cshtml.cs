
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public class ReglasOperativasModel(
    ReglasOperativasApiClient reglasApi) : PageModel
{
    [BindProperty]
    public int MaximoIntentosEntrega { get; set; } = 1;

    [BindProperty]
    public int HorasMinimasEntreIntentos { get; set; }

    [BindProperty]
    public bool RequierePruebaEntrega { get; set; }

    [BindProperty]
    public int DiasMaximosParaDevolucion { get; set; } = 1;

    public bool ReglasEncontradas { get; private set; }

    public DateTimeOffset? VigenciaDesde { get; private set; }

    public string? Error { get; private set; }

    public Guid OperadorIdSesion =>
        Guid.TryParse(
            User.FindFirst("operador_id")?.Value,
            out var id)
            ? id
            : Guid.Empty;

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var operadorId = OperadorIdSesion;

            if (operadorId == Guid.Empty)
            {
                Error = "No hay un operador seleccionado en la sesión.";
                return;
            }

            var reglas = await reglasApi.ObtenerVigentesAsync(
                operadorId, ct);

            if (reglas is null)
                return;

            MaximoIntentosEntrega = reglas.MaximoIntentosEntrega;
            HorasMinimasEntreIntentos = reglas.HorasMinimasEntreIntentos;
            RequierePruebaEntrega = reglas.RequierePruebaEntrega;
            DiasMaximosParaDevolucion = reglas.DiasMaximosParaDevolucion;
            VigenciaDesde = reglas.VigenciaDesde;

            ReglasEncontradas = true;
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo cargar la configuración de reglas operativas.";
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var operadorId = OperadorIdSesion;

        if (operadorId == Guid.Empty)
            return Forbid();

        if (MaximoIntentosEntrega < 1 ||
            HorasMinimasEntreIntentos < 0 ||
            DiasMaximosParaDevolucion < 1)
        {
            Error = "Revisá los valores ingresados.";
            return Page();
        }

        try
        {
            await reglasApi.ActualizarAsync(
                new ActualizarReglasOperativasRequest(
                    operadorId,
                    MaximoIntentosEntrega,
                    HorasMinimasEntreIntentos,
                    RequierePruebaEntrega,
                    DiasMaximosParaDevolucion),
                ct);

            TempData["Mensaje"] =
                "Reglas operativas guardadas correctamente.";

            return RedirectToPage();
        }
        catch (HttpRequestException)
        {
            Error = "No se pudieron guardar las reglas operativas.";
            return Page();
        }
    }
}
