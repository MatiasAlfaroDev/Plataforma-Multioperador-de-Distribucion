
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

/// <summary>
/// Retiros a domicilio: listado, filtros y gestión.
/// </summary>
public sealed class RetirosModel(
    EnviosApiClient api,
    HojasDeRutaApiClient hojas) : PageModel
{
    public static readonly string[] Estados =
    [
        "Solicitado",
        "AsignadoARuta",
        "Retirado",
        "NoRetirado",
        "EnDeposito",
        "EntregadoAlComercio",
        "Cancelado"
    ];

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Desde { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Hasta { get; set; }

    public IReadOnlyList<RetiroTableroItem> Retiros
        { get; private set; } = [];

    public IReadOnlyList<HojaRutaApiDto> HojasDisponibles
        { get; private set; } = [];

    public string? Error { get; private set; }

    public Guid OperadorIdSesion =>
        Guid.TryParse(
            User.FindFirst("operador_id")?.Value,
            out var id)
            ? id
            : Guid.Empty;

    public bool EsDespacho =>
        User.IsInRole("AdministradorOperador") ||
        User.IsInRole("Despachador") ||
        User.IsInRole("Operador");

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Retiros = (await api.ListarRetirosAsync(
                    Estado, Desde, Hasta, ct))
                .OrderBy(r => r.FechaSolicitada)
                .ThenBy(r => r.HoraDesde)
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode is not null
                ? $"No se pudieron cargar los retiros (HTTP {(int)ex.StatusCode})."
                : "No se pudo conectar con la API.";
        }

        if (!EsDespacho || !Retiros.Any(r =>
            r.Estado == "Solicitado"))
            return;

        if (OperadorIdSesion == Guid.Empty)
        {
            Error ??= "No hay un operador seleccionado en la sesión.";
            return;
        }

        try
        {
            HojasDisponibles = (await hojas.ListarAsync(
                    OperadorIdSesion, ct))
                .Where(h =>
                    !h.Despachada &&
                    !h.Rendida &&
                    !h.PendienteDeControl)
                .OrderBy(h => h.Fecha)
                .ThenBy(h => h.HoraSalida)
                .ToList();
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is InvalidOperationException)
        {
            Error ??= $"No se pudieron cargar las hojas de ruta: {ex.Message}";
        }
    }

    public Task<IActionResult> OnPostAsignarARutaAsync(
        Guid retiroId,
        Guid hojaId,
        CancellationToken ct)
    {
        if (!EsDespacho)
            return Task.FromResult<IActionResult>(Forbid());

        if (OperadorIdSesion == Guid.Empty)
            return Task.FromResult<IActionResult>(Forbid());

        if (retiroId == Guid.Empty || hojaId == Guid.Empty)
            return Task.FromResult<IActionResult>(BadRequest());

        return EjecutarAsync(
            () => hojas.AgregarRetiroAsync(
                hojaId, retiroId, ct),
            "Retiro asignado correctamente a la hoja de ruta.");
    }

    public Task<IActionResult> OnPostAccionAsync(
        Guid retiroId,
        string accion,
        CancellationToken ct) =>
        accion is "recibir-deposito"
            or "entregar-comercio"
            or "reprogramar"
            ? EjecutarAsync(
                () => api.GestionarRetiroAsync(
                    retiroId, accion, ct),
                "Retiro actualizado.")
            : Task.FromResult<IActionResult>(BadRequest());

    public Task<IActionResult> OnPostCancelarAsync(
        Guid retiroId,
        string motivo,
        CancellationToken ct) =>
        string.IsNullOrWhiteSpace(motivo)
            ? EjecutarAsync(
                () => throw new InvalidOperationException(
                    "Indicá el motivo de la cancelación."),
                "")
            : EjecutarAsync(
                () => api.CancelarRetiroAsync(
                    retiroId, motivo.Trim(), ct),
                "Retiro cancelado.");

    public Task<IActionResult> OnPostQuitarDeHojaAsync(
        Guid retiroId,
        Guid hojaId,
        CancellationToken ct) =>
        EjecutarAsync(
            () => hojas.QuitarRetiroAsync(
                hojaId, retiroId, ct),
            "Retiro quitado de la hoja de ruta: vuelve a estar solicitado.");

    private async Task<IActionResult> EjecutarAsync(
        Func<Task> accion,
        string exito)
    {
        try
        {
            await accion();
            TempData["MensajeExito"] = exito;
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is InvalidOperationException)
        {
            TempData["MensajeError"] = ex.Message;
        }

        return RedirectToPage(new
        {
            Estado,
            Desde,
            Hasta
        });
    }
}
