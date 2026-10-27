
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class CrearZonaFormulario
{
    [Required(ErrorMessage = "Ingresá el nombre de la zona.")]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el departamento.")]
    [StringLength(50)]
    public string Departamento { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "El traslado debe ser positivo.")]
    public int MinutosTraslado { get; set; } = 15;

    [Required(ErrorMessage = "Indicá al menos una localidad o sector.")]
    public string Coberturas { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indicá al menos una franja.")]
    public string Franjas { get; set; } = string.Empty;
}

public class ZonasModel(
    ZonasApiClient zonasApi) : PageModel
{

    [BindProperty]
    public CrearZonaFormulario Formulario { get; set; } = new();
    public Guid OperadorIdSesion =>
    Guid.TryParse(
        User.FindFirst("operador_id")?.Value,
        out var id)
        ? id
        : Guid.Empty;
    public IReadOnlyList<ZonaListadoItem> Zonas { get; private set; } = [];

    public string? Error { get; private set; }

  public async Task OnGetAsync(CancellationToken ct)
{
    await CargarDatosAsync(ct);
}

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
       if (OperadorIdSesion == Guid.Empty)
        return Forbid();

        var coberturas = ParsearCoberturas(Formulario.Coberturas);
        var franjas = ParsearFranjas(Formulario.Franjas);

        if (!ModelState.IsValid)
        {
            await CargarDatosAsync(ct);
            return Page();
        }

        try
        {
            var solicitud = new CrearZonaRequest(
                OperadorIdSesion,
                Formulario.Nombre.Trim(),
                Formulario.Departamento.Trim(),
                Formulario.MinutosTraslado,
                coberturas,
                franjas);

            await zonasApi.CrearAsync(solicitud, ct);

            TempData["Mensaje"] = "Zona creada correctamente.";

            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo conectar con la API.";
        }

        await CargarDatosAsync(ct);
        return Page();
    }

    private async Task CargarDatosAsync(CancellationToken ct)
{
    try
    {
        if (OperadorIdSesion == Guid.Empty)
        {
            Error = "No hay un operador seleccionado en la sesión.";
            return;
        }

        Zonas = await zonasApi.ListarAsync(OperadorIdSesion, ct);
    }
    catch (HttpRequestException ex)
    {
        Error ??= $"No se pudieron cargar las zonas: {ex.Message}";
    }
} 

    private List<CoberturaZonaRequest> ParsearCoberturas(string? texto)
    {
        var resultado = new List<CoberturaZonaRequest>();

        foreach (var linea in SepararLineas(texto))
        {
            var partes = linea.Split('|', 2);

            var localidad = partes[0].Trim();

            var sector = partes.Length == 2
                ? partes[1].Trim()
                : null;

            if (string.IsNullOrWhiteSpace(localidad) ||
                partes.Length == 2 &&
                string.IsNullOrWhiteSpace(sector))
            {
                ModelState.AddModelError(
                    "Formulario.Coberturas",
                    "Usá 'Localidad' o 'Localidad | Sector'.");
                continue;
            }

            resultado.Add(new CoberturaZonaRequest(
                localidad,
                sector));
        }

        if (resultado.Count == 0)
            ModelState.AddModelError(
                "Formulario.Coberturas",
                "Agregá al menos una cobertura.");

        return resultado;
    }

    private List<FranjaZonaRequest> ParsearFranjas(string? texto)
    {
        var resultado = new List<FranjaZonaRequest>();

        foreach (var linea in SepararLineas(texto))
        {
            var partes = linea.Split('-', 2);

            if (partes.Length != 2 ||
                !TimeOnly.TryParseExact(
                    partes[0].Trim(),
                    "HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var desde) ||
                !TimeOnly.TryParseExact(
                    partes[1].Trim(),
                    "HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var hasta) ||
                desde >= hasta)
            {
                ModelState.AddModelError(
                    "Formulario.Franjas",
                    $"Franja inválida: '{linea}'. Usá HH:mm-HH:mm.");
                continue;
            }

            resultado.Add(new FranjaZonaRequest(desde, hasta));
        }

        if (resultado.Count == 0)
            ModelState.AddModelError(
                "Formulario.Franjas",
                "Agregá al menos una franja.");

        return resultado;
    }

    private static IEnumerable<string> SepararLineas(string? texto)
    {
        return (texto ?? string.Empty)
            .Split(['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }
}
