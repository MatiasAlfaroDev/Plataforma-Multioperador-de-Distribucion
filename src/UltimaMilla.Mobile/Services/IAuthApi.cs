using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>Inicio de sesión contra la API (POST /api/auth/login).</summary>
public interface IAuthApi
{
    /// <summary>Devuelve los datos de la sesión, o null si el email o la contraseña están mal.</summary>
    Task<LoginResponse?> IniciarSesionAsync(string email, string password, CancellationToken ct = default);
}