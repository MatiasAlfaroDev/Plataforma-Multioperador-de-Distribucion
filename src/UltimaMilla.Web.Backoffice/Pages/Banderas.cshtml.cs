using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

/// <summary>Aclaración 3.2: el administrador de la plataforma habilita cada funcionalidad operador por operador.</summary>
[Authorize(Roles = "AdministradorPlataforma")]
public sealed class BanderasModel(BanderasApiClient api) : PageModel
{
    public static readonly (string Clave, string Texto)[] Nombres =
    [
        ("entrega-parcial", "Entrega parcial de bultos"),
        ("retiro-domicilio", "Retiro en domicilio")
    ];

    public BanderasAdminItem Datos { get; private set; } = new([], []);
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Datos = await api.ListarAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Error = $"No se pudieron cargar las banderas: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostAsync(Guid operadorId, string bandera, bool habilitada, CancellationToken ct)
    {
        try
        {
            await api.CambiarAsync(operadorId, bandera, habilitada, ct);
            TempData["Mensaje"] = "Bandera actualizada: todas las réplicas la ven en la próxima consulta.";
        }
        catch (HttpRequestException ex)
        {
            TempData["Mensaje"] = $"No se pudo cambiar: {ex.Message}";
        }

        return RedirectToPage();
    }

    public string Texto(string clave) => Nombres.FirstOrDefault(n => n.Clave == clave).Texto ?? clave;

    public string Operador(Guid id) => Datos.Estado.FirstOrDefault(e => e.OperadorId == id)?.Operador ?? id.ToString();
}
