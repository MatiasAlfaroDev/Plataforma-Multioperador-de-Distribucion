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

    public IReadOnlyList<OperadorItem> Operadores { get; private set; } = [];
    public IReadOnlyList<ZonaItem> Zonas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Operadores = await enviosApi.ListarOperadoresAsync(ct);

        if (OperadorId is not Guid operadorId)
            return;

        Zonas = await tarifasApi.ListarZonasAsync(operadorId, ct);

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
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (OperadorId is not Guid operadorId ||
            ZonaCoberturaId is not Guid zonaId)
        {
            return RedirectToPage();
        }

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
}