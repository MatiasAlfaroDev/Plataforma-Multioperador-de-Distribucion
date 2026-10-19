using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public class TarifasModel(
    TarifasApiClient tarifasApi,
    EnviosApiClient enviosApi) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? OperadorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? ZonaCoberturaId { get; set; }

    [BindProperty]
    public Guid CuadroTarifarioId { get; set; }

    [BindProperty]
    public decimal PrecioBase { get; set; }

    [BindProperty]
    public decimal PrecioPorKg { get; set; }

    [BindProperty]
    public decimal PrecioPorMetroCubico { get; set; }

    [BindProperty]
    public decimal RecargoUrgentePorcentaje { get; set; }

    [BindProperty]
    public decimal BonificacionPorcentaje { get; set; }

    public DateTimeOffset? VigenciaDesde { get; set; }

    public bool TarifaEncontrada { get; private set; }

    public string? Error { get; private set; }

    public IReadOnlyList<OperadorItem> Operadores { get; private set; } = [];
    public IReadOnlyList<ZonaItem> Zonas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Operadores = await enviosApi.ListarOperadoresAsync(ct);

            if (OperadorId is not Guid operadorId)
                return;

            Zonas = await tarifasApi.ListarZonasAsync(operadorId, ct);

            // La zona elegida era de otro operador; se descarta en vez de consultar una combinación que no existe
            if (ZonaCoberturaId is not null && !Zonas.Any(z => z.Id == ZonaCoberturaId))
            {
                ZonaCoberturaId = null;
            }

            if (ZonaCoberturaId is not Guid zonaId)
                return;

            var tarifa = await tarifasApi.ObtenerVigenteAsync(
                operadorId,
                zonaId,
                ct);

            if (tarifa is null)
                return;

            CuadroTarifarioId = tarifa.CuadroTarifarioId;
            PrecioBase = tarifa.PrecioBase;
            PrecioPorKg = tarifa.PrecioPorKg;
            PrecioPorMetroCubico = tarifa.PrecioPorMetroCubico;
            RecargoUrgentePorcentaje = tarifa.RecargoUrgentePorcentaje;
            BonificacionPorcentaje = tarifa.BonificacionPorcentaje;
            VigenciaDesde = tarifa.VigenciaDesde;
            TarifaEncontrada = true;
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo cargar la configuración de tarifas. Probá de nuevo en unos minutos.";
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (OperadorId is not Guid operadorId ||
            ZonaCoberturaId is not Guid zonaId)
        {
            return RedirectToPage();
        }

        try
        {
            await tarifasApi.ActualizarAsync(
                new ActualizarTarifaRequest(
                    operadorId,
                    CuadroTarifarioId,
                    zonaId,
                    PrecioBase,
                    PrecioPorKg,
                    PrecioPorMetroCubico,
                    RecargoUrgentePorcentaje,
                    BonificacionPorcentaje),
                ct);

            TempData["Mensaje"] = "Tarifa actualizada correctamente.";

            return RedirectToPage(new
            {
                operadorId,
                zonaCoberturaId = zonaId
            });
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "No se pudo guardar la tarifa. Revisá los valores y probá de nuevo.";
            return RedirectToPage(new
            {
                operadorId,
                zonaCoberturaId = zonaId
            });
        }
    }
}
