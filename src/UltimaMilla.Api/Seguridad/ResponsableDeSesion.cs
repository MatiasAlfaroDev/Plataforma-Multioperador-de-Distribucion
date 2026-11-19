using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using UltimaMilla.Infrastructure.Identidad;

namespace UltimaMilla.Api.Seguridad;

/// <summary>
/// El responsable que queda en el historial de una acción es el usuario de la sesión, nunca un
/// dato que escribe quien la hace.
/// </summary>
public static class ResponsableDeSesion
{
    public static async Task<string> ObtenerAsync(HttpContext context, UserManager<Usuario> userManager)
    {
        var usuarioId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var usuario = string.IsNullOrWhiteSpace(usuarioId) ? null : await userManager.FindByIdAsync(usuarioId);

        if (usuario is null || !usuario.Activo)
            throw new UnauthorizedAccessException("No se pudo identificar al usuario de la sesión.");

        return !string.IsNullOrWhiteSpace(usuario.NombreCompleto)
            ? usuario.NombreCompleto.Trim()
            : usuario.Email ?? usuario.UserName ?? usuario.Id.ToString();
    }
}
