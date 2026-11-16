
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class HojasDeRutaModel(
    HojasDeRutaApiClient api) : PageModel
{
    public Guid OperadorIdSesion =>
        Guid.TryParse(
            User.FindFirst("operador_id")?.Value,
            out var id)
            ? id
            : Guid.Empty;

    public List<RepartidorRutaApiDto> Repartidores { get; private set; } = [];

    public List<VehiculoRutaApiDto> Vehiculos { get; private set; } = [];

    public List<EnvioRutaApiDto> Envios { get; private set; } = [];

    public List<RetiroRutaApiDto> Retiros { get; private set; } = [];

    public List<DevolucionRutaApiDto> Devoluciones { get; private set; } = [];

    public bool RetiroHabilitado { get; private set; } = true;

    public List<EntregaRetiroRutaApiDto> EntregasRetiros { get; private set; } = [];

    public List<HojaRutaApiDto> Hojas { get; private set; } = [];

    public List<FranjaRutaApiDto> Franjas { get; private set; } = [];

    public string? Error { get; private set; }

    [BindProperty]
    public FormularioHojaRuta Formulario { get; set; } = new();

    
public async Task OnGetAsync(
    Guid? envioId,
    Guid? entregaRetiroId,
    Guid? devolucionId,
    CancellationToken ct)
{
    await CargarAsync(ct);

    // Preseleccionar envío normal.
    if (envioId.HasValue &&
        Envios.Any(x => x.Id == envioId.Value))
    {
        Formulario.EnviosIds = [envioId.Value];
    }

    // Preseleccionar entrega de retiro al comercio.
    if (entregaRetiroId.HasValue &&
        EntregasRetiros.Any(x => x.Id == entregaRetiroId.Value))
    {
        Formulario.EntregasRetirosIds = [entregaRetiroId.Value];
    }

    // Preseleccionar devolución al comercio.
    if (devolucionId.HasValue &&
        Devoluciones.Any(x => x.Id == devolucionId.Value))
    {
        Formulario.DevolucionesIds = [devolucionId.Value];
    }
}

    public async Task<IActionResult> OnPostAsync(
        CancellationToken ct)
    {
        if (Formulario.RepartidorId == Guid.Empty)
        {
            ModelState.AddModelError(
                "",
                "Seleccioná un repartidor.");
        }

        if (Formulario.VehiculoId == Guid.Empty)
        {
            ModelState.AddModelError(
                "",
                "Seleccioná un vehículo.");
        }

        // La hoja puede incluir entregas, retiros,
        // devoluciones al comercio y entregas de retiros.
        if (Formulario.EnviosIds.Count +
            Formulario.RetirosIds.Count +
            Formulario.DevolucionesIds.Count +
            Formulario.EntregasRetirosIds.Count == 0)
        {
            ModelState.AddModelError(
                "",
                "Seleccioná al menos un envío, retiro o devolución.");
        }

        var franjaValida = IntentarObtenerFranja(
            Formulario.FranjaSeleccionada,
            out var horaDesde,
            out var horaHasta);

        if (!franjaValida)
        {
            ModelState.AddModelError(
                nameof(Formulario.FranjaSeleccionada),
                "Seleccioná una franja horaria válida.");
        }

        if (!ModelState.IsValid)
        {
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            var solicitud = new CrearHojaRutaApiRequest(
                OperadorIdSesion,
                Formulario.RepartidorId,
                Formulario.VehiculoId,
                Formulario.Fecha,
                Formulario.HoraSalida,
                horaDesde,
                horaHasta,
                Formulario.MaximoParadas,
                Formulario.EnviosIds,
                Formulario.RetirosIds,
                Formulario.EntregasRetirosIds,
                Formulario.DevolucionesIds);

            var hojaId = await api.CrearAsync(
                solicitud, ct);

            TempData["Exito"] =
                $"Hoja de ruta creada correctamente. ID: {hojaId}";

            return RedirectToPage("/HojasDeRuta");
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is InvalidOperationException)
        {
            Error = ex.Message;

            await CargarAsync(ct);

            return Page();
        }
    }

    public async Task<IActionResult> OnPostDespacharAsync(
        Guid hojaId,
        CancellationToken ct)
    {
        if (OperadorIdSesion == Guid.Empty)
        {
            Error = "No hay un operador seleccionado.";
            await CargarAsync(ct);
            return Page();
        }

        if (hojaId == Guid.Empty)
        {
            Error = "Debe seleccionar una hoja de ruta válida.";
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            var cantidad = await api.DespacharAsync(
                OperadorIdSesion,
                hojaId,
                ct);

            TempData["Exito"] =
                $"Hoja de ruta despachada correctamente. " +
                $"{cantidad} envío(s) actualizado(s).";

            return RedirectToPage("/HojasDeRuta");
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is InvalidOperationException)
        {
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAgregarRetiroAsync(Guid hojaId, Guid retiroId, CancellationToken ct)
    {
        if (hojaId == Guid.Empty || retiroId == Guid.Empty)
        {
            Error = "Elegí el retiro que querés sumar a la hoja.";
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            await api.AgregarRetiroAsync(hojaId, retiroId, ct);
            TempData["Exito"] = "Retiro agregado a la hoja de ruta.";
            return RedirectToPage("/HojasDeRuta");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }

    /// <summary>
    /// Agrega un envío normal a una hoja todavía no despachada.
    /// </summary>
    public async Task<IActionResult> OnPostAgregarEnvioAsync(
        Guid hojaId,
        Guid envioId,
        string? franjaSeleccionada,
        CancellationToken ct)
    {
        if (OperadorIdSesion == Guid.Empty)
        {
            Error = "No hay un operador seleccionado.";
            await CargarAsync(ct);
            return Page();
        }

        if (hojaId == Guid.Empty || envioId == Guid.Empty)
        {
            Error = "Elegí un envío para agregar a la hoja.";
            await CargarAsync(ct);
            return Page();
        }

        if (!IntentarObtenerFranja(
            franjaSeleccionada,
            out var desde,
            out var hasta))
        {
            Error = "Elegí una franja horaria válida.";
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            await api.AgregarEnvioARutaAsync(
                hojaId,
                envioId,
                desde,
                hasta,
                ct);

            TempData["Exito"] =
                "El envío se agregó correctamente a la hoja de ruta.";

            return RedirectToPage("/HojasDeRuta");
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is InvalidOperationException)
        {
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }

    /// <summary>
    /// Agrega un envío urgente a una hoja ya despachada.
    /// </summary>
    public async Task<IActionResult> OnPostAgregarUrgenteAsync(
        Guid hojaId,
        Guid envioUrgenteId,
        string? franjaUrgente,
        CancellationToken ct)
    {
        if (hojaId == Guid.Empty ||
            envioUrgenteId == Guid.Empty)
        {
            Error = "Elegí el envío que querés sumar a la hoja.";
            await CargarAsync(ct);
            return Page();
        }

        if (!IntentarObtenerFranja(
            franjaUrgente,
            out var desde,
            out var hasta))
        {
            Error = "Elegí una franja horaria para el envío urgente.";
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            await api.AgregarEnvioUrgenteAsync(
                hojaId,
                envioUrgenteId,
                desde,
                hasta,
                ct);

            TempData["Exito"] =
                "Envío agregado a la hoja despachada. " +
                "El repartidor recibe un aviso en el teléfono " +
                "y lo levanta del depósito.";

            return RedirectToPage("/HojasDeRuta");
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is InvalidOperationException)
        {
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        try
        {
            var operadorId = OperadorIdSesion;

            if (operadorId == Guid.Empty)
            {
                Error = "No hay un operador seleccionado en la sesión.";
                return;
            }

            var opciones = await api.ObtenerOpcionesAsync(
                operadorId,
                ct);

            Repartidores = opciones.Repartidores;
            Vehiculos = opciones.Vehiculos;
            Envios = opciones.Envios;

            // Cargar devoluciones disponibles al comercio.
            Devoluciones = opciones.Devoluciones ?? [];

            // Funcionalidad de retiros habilitada por bandera.
            RetiroHabilitado =
                (await api.ObtenerBanderasAsync(ct))
                .GetValueOrDefault("retiro-domicilio", true);

            Retiros = RetiroHabilitado
                ? opciones.Retiros ?? []
                : [];

            EntregasRetiros = RetiroHabilitado
                ? opciones.EntregasRetiros ?? []
                : [];

            Franjas = opciones.Franjas;

            Hojas = await api.ListarAsync(
                operadorId,
                ct);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is InvalidOperationException ||
            ex is UnauthorizedAccessException)
        {
            Error ??=
                $"No se pudieron cargar las hojas de ruta: {ex.Message}";
        }
    }

    private static bool IntentarObtenerFranja(
        string? valor,
        out TimeOnly desde,
        out TimeOnly hasta)
    {
        desde = default;
        hasta = default;

        if (string.IsNullOrWhiteSpace(valor))
            return false;

        var partes = valor.Split('|');

        if (partes.Length != 2)
            return false;

        if (!TimeOnly.TryParseExact(
            partes[0],
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out desde))
        {
            return false;
        }

        if (!TimeOnly.TryParseExact(
            partes[1],
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out hasta))
        {
            return false;
        }

        return desde < hasta;
    }

    public sealed class FormularioHojaRuta
    {
        public Guid RepartidorId { get; set; }

        public Guid VehiculoId { get; set; }

        [Required]
        public DateOnly Fecha { get; set; } =
            DateOnly.FromDateTime(DateTime.Today);

        [Required]
        public TimeOnly HoraSalida { get; set; } =
            new(9, 0);

        [Required(ErrorMessage =
            "Seleccioná una franja horaria.")]
        public string FranjaSeleccionada { get; set; } =
            string.Empty;

        [Range(1, 200, ErrorMessage =
            "El máximo de paradas debe estar entre 1 y 200.")]
        public int MaximoParadas { get; set; } = 20;

        public List<Guid> EnviosIds { get; set; } = [];

        public List<Guid> RetirosIds { get; set; } = [];

        public List<Guid> EntregasRetirosIds { get; set; } = [];

        public List<Guid> DevolucionesIds { get; set; } = [];
    }
}
