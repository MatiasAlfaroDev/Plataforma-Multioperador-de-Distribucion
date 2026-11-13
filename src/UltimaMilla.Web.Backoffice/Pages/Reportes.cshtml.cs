
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class ReportesModel(
    ReportesApiClient reportesApi) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateOnly? Desde { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Hasta { get; set; }

    public ResumenEnviosReporte? Resumen { get; private set; }

    public ResumenRetirosReporte? Retiros { get; private set; }

    public MotivosNoEntregaReporte? Motivos { get; private set; }

    public string? Error { get; private set; }

    public ActividadComercialReporte? Actividad { get; private set; }

    public ResultadosRepartidoresReporte? Repartidores { get; private set; }

    public CumplimientoReporteDto? Cumplimiento { get; private set; }

    public DetalleLiquidacionReporte? Liquidacion { get; private set; }

    public PuntualidadReporte? Puntualidad { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        CancellationToken ct)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return Challenge();

        if (!Guid.TryParse(
                User.FindFirst("operador_id")?.Value,
                out var operadorId) ||
            operadorId == Guid.Empty)
        {
            Error = "No hay un operador seleccionado.";
            return Page();
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        Desde ??= new DateOnly(hoy.Year, hoy.Month, 1);
        Hasta ??= hoy;

        if (Desde > Hasta)
        {
            Error = "La fecha desde no puede ser posterior a la fecha hasta.";
            return Page();
        }

        if (Hasta == DateOnly.MaxValue)
        {
            Error = "La fecha hasta está fuera del rango permitido.";
            return Page();
        }

        try
        {
            Resumen = await reportesApi.ObtenerResumenAsync(
                Desde.Value,
                Hasta.Value,
                ct);

            Retiros = await reportesApi.ObtenerResumenRetirosAsync(
                Desde.Value,
                Hasta.Value,
                ct);

            Motivos = await reportesApi.ObtenerMotivosAsync(
                Desde.Value,
                Hasta.Value,
                ct);

            Actividad = await reportesApi.ObtenerActividadAsync(
                Desde.Value,
                Hasta.Value,
                ct);

            Repartidores = await reportesApi.ObtenerRepartidoresAsync(
                Desde.Value,
                Hasta.Value,
                ct);

            Cumplimiento = await reportesApi.ObtenerCumplimientoAsync(
                Desde.Value,
                Hasta.Value,
                ct);

            Liquidacion = await reportesApi.ObtenerLiquidacionAsync(
                Desde.Value,
                Hasta.Value,
                ct);
            
            Puntualidad = await reportesApi.ObtenerPuntualidadAsync(
                Desde.Value,
                Hasta.Value,
                ct);
        }
        catch (HttpRequestException ex)
        {
            Error = $"No se pudieron cargar los reportes (HTTP {(int?)ex.StatusCode}).";
        }
        catch (System.Text.Json.JsonException)
        {
            Error = "La API devolvió un formato de reporte inesperado.";
        }

        return Page();
    }
    
}
