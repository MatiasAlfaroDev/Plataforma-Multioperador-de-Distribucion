using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

/// <summary>
/// RF 22: rendición al regresar al depósito. Muestra lo que tiene que volver de la hoja (bultos no
/// entregados y bultos retirados en domicilios), se escanean al llegar y, si coincide, se cierra la hoja.
/// </summary>
public sealed class RendicionModel(HojasDeRutaApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid HojaId { get; set; }

    [BindProperty]
    public string Escaneados { get; set; } = string.Empty;

    [BindProperty]
    public string Responsable { get; set; } = string.Empty;

    public RendicionApiDto? Rendicion { get; private set; }

    public string? Error { get; private set; }

    public int TotalBultos => Rendicion?.Retornos.Sum(r => r.Bultos.Count) ?? 0;

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Responsable))
            Responsable = User.Identity?.Name ?? string.Empty;

        await CargarAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Un código por línea (o separados por coma), como sale de una pistola lectora.
        var codigos = (Escaneados ?? string.Empty)
    .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        try
        {
            var resultado = await api.RendirAsync(HojaId, codigos, Responsable ?? string.Empty, ct);

            TempData["Exito"] =
                $"Hoja rendida. {resultado.EnviosRetornados} envío(s) volvieron al depósito y " +
                $"{resultado.RetirosRecibidos} retiro(s) quedaron en depósito.";

            return RedirectToPage("/HojasDeRuta");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
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
            Rendicion = await api.ObtenerRendicionAsync(HojaId, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            Error ??= ex.Message;
        }
    }
}
