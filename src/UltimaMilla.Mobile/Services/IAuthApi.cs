using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Inicio de sesión contra la API, con cookie (ASP.NET Identity). Son tres pasos:
/// ① login con email y contraseña, ② listar los operadores del usuario, ③ elegir uno.
/// </summary>
public interface IAuthApi
{
    /// <summary>① Devuelve el usuario, o null si el email o la contraseña están mal.</summary>
    Task<UsuarioLogin?> IniciarSesionAsync(string email, string password, CancellationToken ct = default);

    /// <summary>② Los operadores para los que trabaja el usuario logueado.</summary>
    Task<IReadOnlyList<Membresia>> ListarMembresiasAsync(CancellationToken ct = default);

    /// <summary>③ Elige el operador: la cookie nueva ya lleva el operador y el perfil.</summary>
    Task<Membresia> SeleccionarOperadorAsync(Guid membresiaId, CancellationToken ct = default);
}