
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class DetalleRetiroModel(RetirosApiClient api)
    : PageModel
{
    public RetiroBackofficeDto? Retiro { get; private set; }

    public string? Error { get; private set; }

    [TempData]
    public string? MensajeExito { get; set; }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest();

        try
        {
            Retiro = await api.ObtenerAsync(id, ct);

            if (Retiro is null)
                return NotFound();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
                return NotFound();

            Error = "No se pudo cargar el detalle del retiro.";
        }
        catch (UnauthorizedAccessException)
        {
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRecibirDepositoAsync(
        Guid id,
        CancellationToken ct)
    {
        return await EjecutarAccionAsync(
            id,
            "Retirado",
            api.RecibirEnDepositoAsync,
            "El retiro fue recibido en depósito correctamente.",
            ct);
    }

    public async Task<IActionResult> OnPostEntregarComercioAsync(
        Guid id,
        CancellationToken ct)
    {
        return await EjecutarAccionAsync(
            id,
            "EnDeposito",
            api.EntregarAlComercioAsync,
            "El retiro fue entregado al comercio correctamente.",
            ct);
    }

    private async Task<IActionResult> EjecutarAccionAsync(
        Guid id,
        string estadoRequerido,
        Func<Guid, CancellationToken, Task> accion,
        string mensajeExito,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return BadRequest();

        var resultado = await OnGetAsync(id, ct);

        if (resultado is not PageResult || Retiro is null)
            return resultado;

        if (Error is not null)
            return Page();

        if (Retiro.Estado != estadoRequerido)
        {
            Error = "Esta acción no está disponible para el estado actual del retiro.";
            return Page();
        }

        try
        {
            await accion(id, ct);

            MensajeExito = mensajeExito;

            return RedirectToPage("/DetalleRetiro", new { id });
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode switch
            {
                HttpStatusCode.BadRequest =>
                    $"No se pudo completar la operación. {ex.Message}",

                HttpStatusCode.Forbidden =>
                    "No tenés permisos para realizar esta operación.",

                HttpStatusCode.Unauthorized =>
                    "La sesión expiró. Iniciá sesión nuevamente.",

                HttpStatusCode.NotFound =>
                    "El retiro ya no está disponible.",

                _ => $"Ocurrió un error: {ex.Message}"
            };
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }

        return Page();
    }

    public static string NombreEstado(string estado) =>
        estado switch
        {
            "Solicitado" => "Solicitado",
            "AsignadoARuta" => "Asignado a ruta",
            "Retirado" => "Retirado",
            "NoRetirado" => "No retirado",
            "EnDeposito" => "En depósito",
            "EntregadoAlComercio" => "Entregado al comercio",
            _ => estado
        };

    public static string Direccion(
        DireccionRetiroBackofficeDto domicilio)
    {
        return $"{domicilio.Calle} {domicilio.Numero}, " +
               $"{domicilio.Localidad}, {domicilio.Departamento}";
    }
}
