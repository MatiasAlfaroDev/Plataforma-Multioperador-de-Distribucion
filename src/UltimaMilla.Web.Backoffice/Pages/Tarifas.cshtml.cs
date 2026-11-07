
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public class TarifasModel(
    TarifasApiClient tarifasApi) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? ZonaCoberturaId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string TipoServicio { get; set; } = "Entrega";

    [BindProperty(SupportsGet = true)]
    public string Accion { get; set; } = "editar";

    [BindProperty]
    public string Nombre { get; set; } = string.Empty;

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

    public Guid CuadroTarifarioId { get; private set; }

    public DateTimeOffset? VigenciaDesde { get; private set; }

    public bool TarifaEncontrada { get; private set; }

    public string? Error { get; private set; }

    public IReadOnlyList<ZonaItem> Zonas { get; private set; } = [];

    public Guid OperadorIdSesion =>
        Guid.TryParse(
            User.FindFirst("operador_id")?.Value,
            out var id)
            ? id
            : Guid.Empty;

    public bool EsRetiro =>
        TipoServicio == "RetiroDomicilio";

    public bool EsNuevaTarifa =>
        Accion == "nueva";

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var operadorId = OperadorIdSesion;

            if (operadorId == Guid.Empty)
            {
                Error = "No hay un operador seleccionado en la sesión.";
                return;
            }

            NormalizarSeleccion();

            Zonas = await tarifasApi.ListarZonasAsync(
                operadorId, ct);

            if (ZonaCoberturaId is not null &&
                !Zonas.Any(z => z.Id == ZonaCoberturaId))
            {
                ZonaCoberturaId = null;
            }

            if (ZonaCoberturaId is not Guid zonaId)
                return;

            var tarifa = await tarifasApi.ObtenerVigenteAsync(
                operadorId,
                zonaId,
                TipoServicio,
                ct);

            if (tarifa is null)
            {
                TarifaEncontrada = false;
                return;
            }

            TarifaEncontrada = true;
            CuadroTarifarioId = tarifa.CuadroTarifarioId;
            VigenciaDesde = tarifa.VigenciaDesde;

            if (!EsNuevaTarifa)
            {
                PrecioBase = tarifa.PrecioBase;
                PrecioPorKg = tarifa.PrecioPorKg;
                PrecioPorMetroCubico =
                    tarifa.PrecioPorMetroCubico;

                RecargoUrgentePorcentaje =
                    tarifa.RecargoUrgentePorcentaje;

                BonificacionPorcentaje =
                    tarifa.BonificacionPorcentaje;
            }
        }
        catch (HttpRequestException)
        {
            Error =
                "No se pudo cargar la configuración de tarifas. " +
                "Probá nuevamente.";
        }
    }

    public async Task<IActionResult> OnPostCrearAsync(
        CancellationToken ct)
    {
        var operadorId = OperadorIdSesion;

        if (operadorId == Guid.Empty)
            return Forbid();

        Accion = "nueva";

        if (!SeleccionValida())
        {
            TempData["Error"] =
                "Seleccioná una zona y un tipo de servicio válidos.";

            return RedirectToPage();
        }

        var zonaId = ZonaCoberturaId!.Value;

        if (string.IsNullOrWhiteSpace(Nombre) ||
            Nombre.Trim().Length > 150)
        {
            TempData["Error"] =
                "El nombre de la tarifa es obligatorio " +
                "y no puede superar los 150 caracteres.";

            return Redirigir(zonaId, "nueva");
        }

        if (!PreciosValidos())
        {
            TempData["Error"] =
                "Revisá los importes y porcentajes ingresados.";

            return Redirigir(zonaId, "nueva");
        }

        try
        {
            if (!await ZonaPerteneceAlOperadorAsync(
                    operadorId, zonaId, ct))
            {
                return Forbid();
            }

            var existente = await tarifasApi.ObtenerVigenteAsync(
                operadorId,
                zonaId,
                TipoServicio,
                ct);

            if (existente is not null)
            {
                TempData["Error"] =
                    "Ya existe una tarifa vigente para " +
                    "esta zona y servicio. Podés editarla.";

                return Redirigir(zonaId, "editar");
            }

            await tarifasApi.CrearAsync(
                new CrearTarifaRequest(
                    operadorId,
                    zonaId,
                    Nombre.Trim(),
                    TipoServicio,
                    PrecioBase,
                    PrecioPorKg,
                    PrecioPorMetroCubico,
                    EsRetiro ? 0m : RecargoUrgentePorcentaje,
                    BonificacionPorcentaje),
                ct);

            TempData["Mensaje"] =
                "Tarifa creada correctamente.";

            return Redirigir(zonaId, "editar");
        }
        catch (HttpRequestException)
        {
            TempData["Error"] =
                "No se pudo crear la tarifa. " +
                "Verificá los datos o si ya existe una vigente.";

            return Redirigir(zonaId, "nueva");
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] =
                "No se pudo completar la operación. " +
                "Revisá la sesión e intentá nuevamente.";

            return Redirigir(zonaId, "nueva");
        }
    }

    public async Task<IActionResult> OnPostActualizarAsync(
        CancellationToken ct)
    {
        var operadorId = OperadorIdSesion;

        if (operadorId == Guid.Empty)
            return Forbid();

        Accion = "editar";

        if (!SeleccionValida())
        {
            TempData["Error"] =
                "Seleccioná una zona y un tipo de servicio válidos.";

            return RedirectToPage();
        }

        var zonaId = ZonaCoberturaId!.Value;

        if (!PreciosValidos())
        {
            TempData["Error"] =
                "Revisá los importes y porcentajes ingresados.";

            return Redirigir(zonaId, "editar");
        }

        try
        {
            if (!await ZonaPerteneceAlOperadorAsync(
                    operadorId, zonaId, ct))
            {
                return Forbid();
            }

            // Siempre recuperamos el cuadro desde la API.
            // No confiamos en un ID enviado desde el formulario.
            var tarifa = await tarifasApi.ObtenerVigenteAsync(
                operadorId,
                zonaId,
                TipoServicio,
                ct);

            if (tarifa is null)
            {
                TempData["Error"] =
                    "No existe una tarifa vigente para editar. " +
                    "Primero tenés que crearla.";

                return Redirigir(zonaId, "nueva");
            }

            await tarifasApi.ActualizarAsync(
                new ActualizarTarifaRequest(
                    operadorId,
                    tarifa.CuadroTarifarioId,
                    zonaId,
                    PrecioBase,
                    PrecioPorKg,
                    PrecioPorMetroCubico,
                    EsRetiro ? 0m : RecargoUrgentePorcentaje,
                    BonificacionPorcentaje,
                    TipoServicio),
                ct);

            TempData["Mensaje"] =
                "Nueva versión tarifaria guardada correctamente.";

            return Redirigir(zonaId, "editar");
        }
        catch (HttpRequestException)
        {
            TempData["Error"] =
                "No se pudo actualizar la tarifa. " +
                "Revisá los datos e intentá nuevamente.";

            return Redirigir(zonaId, "editar");
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] =
                "No se pudo completar la operación. " +
                "Revisá la sesión e intentá nuevamente.";

            return Redirigir(zonaId, "editar");
        }
    }

    private void NormalizarSeleccion()
    {
        if (TipoServicio is not (
            "Entrega" or "RetiroDomicilio"))
        {
            TipoServicio = "Entrega";
        }

        if (Accion is not ("nueva" or "editar"))
        {
            Accion = "editar";
        }
    }

    private bool SeleccionValida()
    {
        return ZonaCoberturaId is Guid id &&
               id != Guid.Empty &&
               TipoServicio is (
                   "Entrega" or "RetiroDomicilio");
    }

    private bool PreciosValidos()
    {
        return PrecioBase >= 0 &&
               PrecioPorKg >= 0 &&
               PrecioPorMetroCubico >= 0 &&
               BonificacionPorcentaje is >= 0 and <= 100 &&
               (EsRetiro || RecargoUrgentePorcentaje >= 0);
    }

    private async Task<bool> ZonaPerteneceAlOperadorAsync(
        Guid operadorId,
        Guid zonaId,
        CancellationToken ct)
    {
        var zonas = await tarifasApi.ListarZonasAsync(
            operadorId, ct);

        return zonas.Any(z => z.Id == zonaId);
    }

    private IActionResult Redirigir(
        Guid zonaId,
        string accion)
    {
        return RedirectToPage(new
        {
            zonaCoberturaId = zonaId,
            tipoServicio = TipoServicio,
            accion
        });
    }
}
