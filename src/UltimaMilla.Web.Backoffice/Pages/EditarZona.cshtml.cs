using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

/// <summary>
/// Edición de una zona de cobertura (RF 2): mismo formulario que el alta, cargado con
/// los datos actuales. Las coberturas y franjas que se guardan reemplazan a las anteriores.
/// </summary>
public class EditarZonaModel(ZonasApiClient zonasApi) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    // Mismos campos y validaciones que el formulario de alta.
    [BindProperty]
    public CrearZonaFormulario Formulario { get; set; } = new();

    public string? Error { get; private set; }

    public Guid OperadorIdSesion =>
        Guid.TryParse(User.FindFirst("operador_id")?.Value, out var id)
            ? id
            : Guid.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (OperadorIdSesion == Guid.Empty)
        {
            Error = "No hay un operador seleccionado en la sesión.";
            return Page();
        }

        try
        {
            var zona = await zonasApi.ObtenerAsync(OperadorIdSesion, Id, ct);
            if (zona is null)
                return NotFound();

            Formulario = new CrearZonaFormulario
            {
                Nombre = zona.Nombre,
                Departamento = zona.Departamento,
                MinutosTraslado = zona.MinutosTraslado ?? 15,
                Coberturas = FormatoZonas.EscribirCoberturas(zona.Coberturas),
                Franjas = FormatoZonas.EscribirFranjas(zona.Franjas)
            };
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo conectar con la API.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (OperadorIdSesion == Guid.Empty)
            return Forbid();

        var errores = new List<string>();
        var coberturas = FormatoZonas.LeerCoberturas(Formulario.Coberturas, errores);
        var franjas = FormatoZonas.LeerFranjas(Formulario.Franjas, errores);
        foreach (var error in errores)
            ModelState.AddModelError(string.Empty, error);

        if (!ModelState.IsValid)
            return Page();

        try
        {
            await zonasApi.EditarAsync(
                Id,
                new EditarZonaRequest(
                    OperadorIdSesion,
                    Formulario.Nombre.Trim(),
                    Formulario.Departamento.Trim(),
                    Formulario.MinutosTraslado,
                    coberturas,
                    franjas),
                ct);

            TempData["Mensaje"] = "Zona actualizada correctamente.";
            return RedirectToPage("/Zonas");
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo conectar con la API.";
        }

        return Page();
    }
}
