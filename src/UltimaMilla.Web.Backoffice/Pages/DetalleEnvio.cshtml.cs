using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public class DetalleEnvioModel(EnviosApiClient api) : PageModel
{
    public DetalleEnvioItem? Envio { get; private set; }
    public IReadOnlyList<DiscrepanciaRecepcionItem> Discrepancias { get; private set; } = [];

    public string? Error { get; private set; }
    public string? ErrorAccion { get; private set; }

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
            Discrepancias = await api.ListarDiscrepanciasAsync(id, ct);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound();

            Error = "No se pudo obtener el detalle del envío.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeclararExtravioAsync(
        Guid id,
        string justificacion,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest();

        // El responsable es quien tiene la sesión iniciada.
        var responsable = User.Identity?.Name;
        string? error = null;

        if (string.IsNullOrWhiteSpace(responsable))
        {
            error = "No se pudo identificar al usuario de la sesión.";
        }
        else if (string.IsNullOrWhiteSpace(justificacion))
        {
            error = "Indicá la justificación del extravío.";
        }
        else
        {
            try
            {
                await api.DeclararExtravioAsync(
                    id, justificacion.Trim(), responsable, ct);

                return RedirectToPage("/DetalleEnvio", new { id });
            }
            catch (HttpRequestException ex)
            {
                error = ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "No tenés permisos para declarar el extravío."
                    : "No se pudo declarar el extravío: " + ex.Message;
            }
        }

        var resultado = await OnGetAsync(id, ct);
        ErrorAccion = error;
        return resultado;
    }

    public async Task<IActionResult> OnPostReaparicionAsync(
        Guid id,
        List<string> identificaciones,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest();

        // El responsable es quien tiene la sesión iniciada.
        var responsable = User.Identity?.Name;
        string? error = null;

        if (string.IsNullOrWhiteSpace(responsable))
        {
            error = "No se pudo identificar al usuario de la sesión.";
        }
        else if (identificaciones is null || identificaciones.Count == 0)
        {
            error = "Marcá los bultos encontrados.";
        }
        else
        {
            try
            {
                await api.RegistrarReaparicionAsync(
                    id, identificaciones, responsable, ct);

                return RedirectToPage("/DetalleEnvio", new { id });
            }
            catch (HttpRequestException ex)
            {
                error = ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "No tenés permisos para registrar la reaparición."
                    : "No se pudo registrar la reaparición: " + ex.Message;
            }
        }

        var resultado = await OnGetAsync(id, ct);
        ErrorAccion = error;
        return resultado;
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

        public async Task<IActionResult> OnPostRetornarDepositoAsync(
            Guid id,
            string responsable,
            List<string> identificaciones,
            CancellationToken ct)
        {
            if (id == Guid.Empty)
                return BadRequest();

            if (string.IsNullOrWhiteSpace(responsable))
            {
                Error = "Debe indicar el responsable.";
            }
            else if (identificaciones is null ||
                    identificaciones.Count == 0)
            {
                Error = "Debe seleccionar los bultos recibidos.";
            }
            else
            {
                try
                {
                    await api.RetornarADepositoAsync(
                        id,
                        identificaciones,
                        responsable.Trim(),
                        ct);

                    return RedirectToPage("/DetalleEnvio", new { id });
                }
                catch (HttpRequestException ex)
                {
                    Error = ex.StatusCode switch
                    {
                        System.Net.HttpStatusCode.BadRequest =>
                            "No se pudo registrar el retorno. Verificá los bultos y el estado del envío.",

                        System.Net.HttpStatusCode.Forbidden =>
                            "No tenés permisos para registrar este retorno.",

                        _ => "Ocurrió un error al retornar el envío al depósito."
                    };
                }
            }

            // Recuperamos el detalle para mostrar nuevamente
            // el formulario junto al mensaje de error.
            var error = Error;
            var resultado = await OnGetAsync(id, ct);
            Error = error ?? Error;

            return resultado;
        }
}