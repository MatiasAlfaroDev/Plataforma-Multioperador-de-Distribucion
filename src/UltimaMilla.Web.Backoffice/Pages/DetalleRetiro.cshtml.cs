
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class DetalleRetiroModel(EnviosApiClient api) : PageModel
{
    public DetalleRetiroItem? Retiro { get; private set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest();

        try
        {
            Retiro = await api.ObtenerDetalleRetiroAsync(id, ct);

            if (Retiro is null)
                return NotFound();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound();

            Error = "No se pudo obtener el detalle del retiro.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGestionarAsync(
        Guid id,
        string accion,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest();

        if (accion is not (
            "reprogramar" ))
        {
            return BadRequest();
        }

        try
        {
            await api.GestionarRetiroAsync(id, accion, ct);

            TempData["MensajeExito"] =
                "El retiro se actualizó correctamente.";

            return RedirectToPage("/DetalleRetiro", new { id });
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden =>
                    "No tenés permisos para realizar esta operación.",

                System.Net.HttpStatusCode.BadRequest =>
                    "La operación no está permitida en el estado actual.",

                System.Net.HttpStatusCode.Conflict =>
                    "El estado del retiro cambió. Actualizá la página.",

                _ => $"No se pudo actualizar el retiro: {ex.Message}"
            };

            try
            {
                Retiro = await api.ObtenerDetalleRetiroAsync(id, ct);
            }
            catch (HttpRequestException)
            {
                // Se mantiene el mensaje del error original.
            }

            return Page();
        }
    }
}
