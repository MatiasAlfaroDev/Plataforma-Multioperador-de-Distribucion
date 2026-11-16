using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public class DetalleEnvioModel(EnviosApiClient api) : PageModel
{
    public DetalleEnvioItem? Envio { get; private set; }
    public IReadOnlyList<DiscrepanciaRecepcionItem> Discrepancias { get; private set; } = [];

    public string? Error { get; private set; }

    // Mismos estados que MaquinaEstadosEnvio: cualquiera no final puede declararse extraviado.
    private static readonly string[] EstadosQuePermitenExtravio =
    [
        "EnDeposito", "AsignadoARuta", "EnTransito", "NoEntregado",
        "EntregadoParcialmente", "Reprogramado", "EnDevolucion"
    ];

    public bool PuedeDeclararExtravio =>
        Envio is not null && EstadosQuePermitenExtravio.Contains(Envio.Estado) &&
        PuedeGestionarDespacho;

    public bool PuedeRegistrarReaparicion =>
        Envio?.Estado == "Extraviado" &&
        (PuedeGestionarDespacho || User.IsInRole("OperarioDeposito"));

    private bool PuedeGestionarDespacho =>
        User.IsInRole("AdministradorOperador") || User.IsInRole("Despachador") || User.IsInRole("Operador");

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

    public Task<IActionResult> OnPostDeclararExtravioAsync(
        Guid id, string justificacion, string responsable, CancellationToken ct) =>
        EjecutarAsync(id, ct,
            string.IsNullOrWhiteSpace(justificacion) || string.IsNullOrWhiteSpace(responsable)
                ? "Indicá la justificación y el responsable."
                : null,
            () => api.DeclararExtravioAsync(id, justificacion.Trim(), responsable.Trim(), ct));

    public Task<IActionResult> OnPostRegistrarReaparicionAsync(
        Guid id, string responsable, List<string> identificaciones, CancellationToken ct) =>
        EjecutarAsync(id, ct,
            string.IsNullOrWhiteSpace(responsable) ? "Indicá el responsable."
            : identificaciones is null || identificaciones.Count == 0 ? "Escaneá los bultos que reaparecieron."
            : null,
            () => api.RegistrarReaparicionAsync(id, identificaciones, responsable.Trim(), ct));

    private async Task<IActionResult> EjecutarAsync(
        Guid id, CancellationToken ct, string? errorDeValidacion, Func<Task> accion)
    {
        if (id == Guid.Empty)
            return BadRequest();

        var error = errorDeValidacion;
        if (error is null)
        {
            try
            {
                await accion();
                return RedirectToPage("/DetalleEnvio", new { id });
            }
            catch (HttpRequestException ex)
            {
                error = ex.Message;
            }
        }

        var resultado = await OnGetAsync(id, ct);
        Error = error;
        return resultado;
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