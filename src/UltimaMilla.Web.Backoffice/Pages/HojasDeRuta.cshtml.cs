
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

    public List<HojaRutaApiDto> Hojas { get; private set; } = [];

    public List<FranjaRutaApiDto> Franjas { get; private set; } = [];

    public string? Error { get; private set; }

    [BindProperty]
    public FormularioHojaRuta Formulario { get; set; } = new();

 public async Task OnGetAsync(
    Guid? envioId,
    CancellationToken ct)
{
    await CargarAsync(ct);

    if (envioId.HasValue &&
        Envios.Any(x => x.Id == envioId.Value))
    {
        Formulario.EnviosIds = [envioId.Value];
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

        if (Formulario.EnviosIds is null ||
            Formulario.EnviosIds.Count == 0)
        {
            ModelState.AddModelError(
                "",
                "Seleccioná al menos un envío.");
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
                Formulario.EnviosIds);

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
            operadorId, ct);

        Repartidores = opciones.Repartidores;
        Vehiculos = opciones.Vehiculos;
        Envios = opciones.Envios;
        Franjas = opciones.Franjas;

        Hojas = await api.ListarAsync(operadorId, ct);
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

        [Required(ErrorMessage = "Seleccioná una franja horaria.")]
        public string FranjaSeleccionada { get; set; } =
            string.Empty;

        [Range(1, 200, ErrorMessage =
            "El máximo de paradas debe estar entre 1 y 200.")]
        public int MaximoParadas { get; set; } = 20;

        public List<Guid> EnviosIds { get; set; } = [];
    }
}
