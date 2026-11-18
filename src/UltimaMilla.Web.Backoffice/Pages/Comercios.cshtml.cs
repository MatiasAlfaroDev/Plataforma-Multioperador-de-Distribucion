using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

[Authorize(Roles = "AdministradorOperador")]
public sealed class ComerciosModel(VinculacionesApiClient api) : PageModel
{
    public IReadOnlyList<ComercioGestionItem> Comercios { get; private set; } = [];

    public string? Error { get; private set; }

    public string? Mensaje { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Mensaje = TempData["Mensaje"] as string;
        await CargarAsync(ct);
    }

    public Task<IActionResult> OnPostBajaAsync(Guid id, CancellationToken ct) =>
        EjecutarAsync(
            () => api.DarDeBajaAsync(id, ct),
            "Comercio dado de baja. No podrá crear envíos ni retiros nuevos.",
            "dar de baja el comercio",
            ct);

    public Task<IActionResult> OnPostReactivarAsync(Guid id, CancellationToken ct) =>
        EjecutarAsync(
            () => api.ReactivarAsync(id, ct),
            "Comercio reactivado.",
            "reactivar el comercio",
            ct);

    public Task<IActionResult> OnPostDireccionAsync(
        Guid id,
        string calle,
        string numero,
        string localidad,
        string departamento,
        CancellationToken ct) =>
        EjecutarAsync(
            () => api.ModificarDireccionAsync(id, calle, numero, localidad, departamento, ct),
            "Dirección de recepción actualizada.",
            "modificar la dirección",
            ct);

    private async Task<IActionResult> EjecutarAsync(
        Func<Task> accion,
        string mensajeOk,
        string descripcion,
        CancellationToken ct)
    {
        try
        {
            await accion();
            TempData["Mensaje"] = mensajeOk;
            return RedirectToPage();
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            Error = $"No se pudo {descripcion}: {ex.Message}";
            await CargarAsync(ct);
            return Page();
        }
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        try
        {
            Comercios = await api.ListarComerciosAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Error ??= $"No se pudieron cargar los comercios: {ex.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            Error ??= "La sesión no está disponible. Iniciá sesión nuevamente.";
        }
    }
}
