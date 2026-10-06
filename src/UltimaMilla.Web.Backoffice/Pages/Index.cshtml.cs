using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class IndexModel(EnviosApiClient api) : PageModel
{
    /// <summary>
    /// Provisorio hasta el login del 15/10: el operador se elige en pantalla.
    /// Después saldrá del token del usuario y este filtro desaparece.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public Guid? OperadorId { get; set; }

    public IReadOnlyList<OperadorItem> Operadores { get; private set; } = [];
    public IReadOnlyList<EnvioItem> Envios { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Operadores = await api.ListarOperadoresAsync(ct);
            Envios = await api.ListarEnviosAsync(OperadorId, ct);
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo conectar con la API.";
        }
    }

    public string ColorDe(Guid operadorId) =>
        Operadores.FirstOrDefault(o => o.Id == operadorId)?.ColorPrimario ?? "#999";
}
