using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

// Letra 5.1, requerimiento 6: gestión de vehículos con su capacidad de carga. La política se fija en Program.cs.
public sealed class VehiculosModel(VehiculosApiClient api) : PageModel
{
    public IReadOnlyList<VehiculoAdminDto> Vehiculos { get; private set; } = [];
    public string? Error { get; private set; }

    [BindProperty]
    public FormularioVehiculo Formulario { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            await api.AltaAsync(new AltaVehiculo(
                Formulario.Matricula, Formulario.CapacidadPesoKg, Formulario.CapacidadVolumenM3), ct);
            TempData["Mensaje"] = $"Vehículo {Formulario.Matricula.Trim().ToUpperInvariant()} dado de alta.";
            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            Error = $"No se pudo dar de alta: {ex.Message}";
            await CargarAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEstadoAsync(Guid id, bool activo, CancellationToken ct)
    {
        try
        {
            await api.CambiarEstadoAsync(id, activo, ct);
            TempData["Mensaje"] = activo ? "Vehículo reactivado." : "Vehículo dado de baja.";
            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            Error = $"No se pudo cambiar el estado: {ex.Message}";
            await CargarAsync(ct);
            return Page();
        }
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        try
        {
            Vehiculos = await api.ListarAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Error ??= $"No se pudo cargar la flota: {ex.Message}";
        }
    }

    public sealed class FormularioVehiculo
    {
        [Required(ErrorMessage = "La matrícula es obligatoria.")]
        public string Matricula { get; set; } = string.Empty;

        [Range(0.01, 100000, ErrorMessage = "La capacidad de peso debe ser mayor que cero.")]
        public decimal CapacidadPesoKg { get; set; }

        [Range(0.01, 1000, ErrorMessage = "La capacidad de volumen debe ser mayor que cero.")]
        public decimal CapacidadVolumenM3 { get; set; }
    }
}
