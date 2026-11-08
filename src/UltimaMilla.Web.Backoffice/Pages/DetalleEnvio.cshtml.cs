using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public class DetalleEnvioModel(EnviosApiClient api) : PageModel
{
    public DetalleEnvioItem? Envio { get; private set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest();

        try
        {
            Envio = await api.ObtenerDetalleEnvioAsync(id, ct);

            if (Envio is null)
                return NotFound();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound();

            Error = "No se pudo obtener el detalle del envío.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostIniciarDevolucionAsync(
    Guid id,
    string responsable,
    CancellationToken ct)
{
    if (id == Guid.Empty)
        return BadRequest();

    if (string.IsNullOrWhiteSpace(responsable))
    {
        Error = "Debe indicar un responsable.";
        return await OnGetAsync(id, ct);
    }

    try
    {
        await api.IniciarDevolucionAsync(
            id,
            responsable.Trim(),
            ct);

        return RedirectToPage("/DetalleEnvio", new { id });
    }
    catch (HttpRequestException ex)
    {
        Error = ex.StatusCode switch
        {
            System.Net.HttpStatusCode.BadRequest =>
                "No se pudo iniciar la devolución. Verificá las condiciones del envío.",

            System.Net.HttpStatusCode.Forbidden =>
                "No tenés permisos para iniciar esta devolución.",

            _ => "Ocurrió un error al iniciar la devolución."
        };

        Envio = await api.ObtenerDetalleEnvioAsync(id, ct);
        return Page();
    }
}
}