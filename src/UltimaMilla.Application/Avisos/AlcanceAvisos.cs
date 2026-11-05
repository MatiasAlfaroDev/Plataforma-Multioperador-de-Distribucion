using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos;

/// <summary>
/// Define qué avisos y suscripciones puede ver la sesión actual.
/// </summary>
public static class AlcanceAvisos
{
    /// <summary>
    /// Resuelve el alcance de avisos según el perfil del usuario autenticado.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Si no hay sesión o no tiene permisos.</exception>
    public static (Guid OperadorId, Guid? CuentaComercialId) Resolver(ITenantContext sesion)
    {
        if (!sesion.EstaAutenticado || sesion.OperadorId is null)
            throw new UnauthorizedAccessException("Debe iniciar sesión y seleccionar un operador.");

        return sesion.Perfil switch
        {
            Perfiles.AdministradorOperador or Perfiles.Operador => (sesion.OperadorId.Value, null),
            Perfiles.Comercio => (sesion.OperadorId.Value, sesion.ComercioId ?? throw new UnauthorizedAccessException("No tiene una cuenta comercial seleccionada.")),
            _ => throw new UnauthorizedAccessException("No tiene permisos sobre los avisos.")
        };
    }
}
