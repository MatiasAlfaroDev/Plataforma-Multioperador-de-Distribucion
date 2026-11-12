using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class IndexModel(EnviosApiClient api) : PageModel
{
    public IReadOnlyList<EnvioItem> Envios { get; private set; } = [];

    public IReadOnlyList<RetiroTableroItem> Retiros { get; private set; } = [];

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
            Retiros = await api.ListarRetirosAsync(ct);
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

    public async Task<IActionResult> OnPostRetiroAsync(Guid retiroId, string accion, CancellationToken ct)
    {
        if (accion is not ("recibir-deposito" or "entregar-comercio" or "reprogramar"))
            return BadRequest();

        try
        {
            await api.GestionarRetiroAsync(retiroId, accion, ct);
            TempData["MensajeExito"] = "Retiro actualizado.";
        }
        catch (HttpRequestException ex)
        {
            TempData["MensajeExito"] = ex.Message;
        }

        return RedirectToPage();
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

    public async Task<IActionResult> OnPostReprogramarAsync(
        Guid envioId,
        string responsable,
        CancellationToken ct)
    {
        if (envioId == Guid.Empty || string.IsNullOrWhiteSpace(responsable))
        {
            await CargarDatosAsync(ct);
            Error = "Seleccioná un envío e ingresá el responsable de la reprogramación.";
            return Page();
        }

        Guid? solicitudId = null;

        try
        {
            solicitudId = await api.SolicitarReprogramacionAsync(envioId, ct);
            await api.ConfirmarReprogramacionAsync(
                solicitudId.Value, responsable.Trim(), ct);

            TempData["MensajeExito"] = "El envío fue reprogramado correctamente. Ya podés asignarlo a una nueva ruta.";
            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            await CargarDatosAsync(ct);
            var etapa = solicitudId.HasValue
                ? $"La solicitud {solicitudId.Value} fue creada, pero no se pudo confirmar. No vuelvas a solicitarla: revisá la solicitud pendiente."
                : "No se pudo crear la solicitud de reprogramación.";
            Error = $"{etapa} Detalle: {ex.Message}";
            return Page();
        }
        catch (InvalidOperationException ex)
        {
            await CargarDatosAsync(ct);
            Error = ex.Message;
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            await CargarDatosAsync(ct);
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
            return Page();
        }
    }

}
