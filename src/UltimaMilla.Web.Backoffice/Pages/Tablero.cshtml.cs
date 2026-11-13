using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimaMilla.Web.Backoffice.Pages;

/// <summary>
/// Tablero de operación en vivo (RF 29). La página es solo el armazón: el navegador pide la foto a /api/tablero
/// y escucha el hub /api/hubs/tablero, los dos en la API detrás de Traefik y con la misma cookie de sesión.
/// </summary>
public sealed class TableroModel : PageModel
{
}
