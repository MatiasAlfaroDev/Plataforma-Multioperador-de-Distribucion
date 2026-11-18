
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

[Authorize(Roles = "AdministradorOperador")]
public sealed class SolicitudesComercialesModel(
    VinculacionesApiClient api) : PageModel
{
    public IReadOnlyList<SolicitudComercialItem> Solicitudes { get; private set; } = [];

    // RF 1: comercios ya aceptados, para darlos de baja o reactivarlos.
    public IReadOnlyList<CuentaComercialItem> Aceptados { get; private set; } = [];

    public string? Error { get; private set; }

    public string? Mensaje { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Mensaje = TempData["Mensaje"] as string;
        await CargarSolicitudesAsync(ct);
    }

    public async Task<IActionResult> OnPostAceptarAsync(
        Guid id,
        CancellationToken ct)
    {
        try
        {
            await api.AceptarAsync(id, ct);

            TempData["Mensaje"] = "Solicitud aceptada correctamente.";

            return RedirectToPage();
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            Error = $"No se pudo aceptar la solicitud: {ex.Message}";
            await CargarSolicitudesAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRechazarAsync(
        Guid id,
        CancellationToken ct)
    {
        try
        {
            await api.RechazarAsync(id, ct);

            TempData["Mensaje"] = "Solicitud rechazada correctamente.";

            return RedirectToPage();
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            Error = $"No se pudo rechazar la solicitud: {ex.Message}";
            await CargarSolicitudesAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEstadoAsync(
        Guid id,
        bool activa,
        CancellationToken ct)
    {
        try
        {
            await api.CambiarEstadoAsync(id, activa, ct);
            TempData["Mensaje"] = activa ? "Comercio reactivado." : "Comercio dado de baja.";
            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            Error = $"No se pudo cambiar el estado del comercio: {ex.Message}";
            await CargarSolicitudesAsync(ct);
            return Page();
        }
    }

    private async Task CargarSolicitudesAsync(CancellationToken ct)
    {
        try
        {
            Solicitudes = await api.ListarPendientesAsync(ct);
            Aceptados = (await api.ListarCuentasAsync(ct)).Where(c => c.Aceptada).ToList();
        }
        catch (HttpRequestException ex)
        {
            Error ??= $"No se pudieron cargar las solicitudes: {ex.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            Error ??= "La sesión no está disponible. Iniciá sesión nuevamente.";
        }
    }
}
