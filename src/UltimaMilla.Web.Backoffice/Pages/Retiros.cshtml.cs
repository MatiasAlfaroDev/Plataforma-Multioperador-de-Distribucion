
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Web.Backoffice.Services;

namespace UltimaMilla.Web.Backoffice.Pages;

public sealed class RetirosModel(RetirosApiClient api) : PageModel
{
    public IReadOnlyList<RetiroBackofficeDto> Retiros { get; private set; }
        = [];

    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Retiros = (await api.ListarAsync(ct))
                .OrderByDescending(r => r.SolicitadoEn)
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            Error = ex.StatusCode is not null
                ? $"No se pudieron cargar los retiros (HTTP {(int)ex.StatusCode})."
                : "No se pudo conectar con la API.";
        }
        catch (UnauthorizedAccessException)
        {
            Error = "La sesión no está disponible. Iniciá sesión nuevamente.";
        }
    }

    public static string FormatearDireccion(
        DireccionRetiroBackofficeDto direccion)
    {
        return $"{direccion.Calle} {direccion.Numero}, " +
               $"{direccion.Localidad}, {direccion.Departamento}";
    }

    public static string NombreEstado(string estado) =>
        estado switch
        {
            "Solicitado" => "Solicitado",
            "AsignadoARuta" => "Asignado a ruta",
            "Retirado" => "Retirado",
            "NoRetirado" => "No retirado",
            "EnDeposito" => "En depósito",
            "EntregadoAlComercio" => "Entregado al comercio",
            _ => estado
        };

    public static string ClaseEstado(string estado) =>
        estado switch
        {
            "Solicitado" => "estado-pendiente",
            "AsignadoARuta" => "estado-asignado",
            "Retirado" => "estado-retirado",
            "NoRetirado" => "estado-fallido",
            "EnDeposito" => "estado-deposito",
            "EntregadoAlComercio" => "estado-entregado",
            _ => "estado-pendiente"
        };
}
