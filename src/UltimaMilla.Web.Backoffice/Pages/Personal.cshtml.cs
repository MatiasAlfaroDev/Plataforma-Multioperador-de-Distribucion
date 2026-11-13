using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

// Letra 3.3: el administrador del operador da de alta a su personal. La política se fija en Program.cs.
public sealed class PersonalModel(PersonalApiClient api) : PageModel
{
    public static readonly (string Valor, string Texto)[] Perfiles =
    [
        ("Despachador", "Despachador"),
        ("OperarioDeposito", "Operario de depósito"),
        ("Repartidor", "Repartidor"),
        ("AdministradorOperador", "Administrador")
    ];

    public IReadOnlyList<PersonalItem> Personal { get; private set; } = [];
    public string? Error { get; private set; }

    [BindProperty]
    public FormularioPersonal Formulario { get; set; } = new();

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
            await api.AltaAsync(new AltaPersonal(
                Formulario.NombreCompleto, Formulario.Email, Formulario.Password,
                Formulario.Perfil, Formulario.Telefono), ct);
            TempData["Mensaje"] = $"{Formulario.Email} dado de alta.";
            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            Error = $"No se pudo dar de alta: {ex.Message}";
            await CargarAsync(ct);
            return Page();
        }
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        try
        {
            Personal = await api.ListarAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Error ??= $"No se pudo cargar el personal: {ex.Message}";
        }
    }

    public static string TextoPerfil(string perfil) =>
        Perfiles.FirstOrDefault(p => p.Valor == perfil).Texto ?? perfil;

    public sealed class FormularioPersonal
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio."), EmailAddress(ErrorMessage = "Email no válido.")]
        public string Email { get; set; } = string.Empty;

        // Opcional si la persona ya tiene cuenta (por ejemplo, trabaja para otro operador).
        public string? Password { get; set; }

        [Required]
        public string Perfil { get; set; } = "Despachador";

        public string? Telefono { get; set; }
    }
}
