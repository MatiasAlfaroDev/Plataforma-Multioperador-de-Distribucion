namespace UltimaMilla.Mobile.Models;

/// <summary>① Lo que la app manda a POST /api/auth/login.</summary>
public sealed record LoginRequest(
    string Email,
    string Password);

/// <summary>① Lo que devuelve el login (además, la API deja una cookie de sesión).</summary>
public sealed record UsuarioLogin(
    Guid Id,
    string Email,
    string NombreCompleto);

/// <summary>② Un operador para el que trabaja el usuario (GET /api/auth/membresias).</summary>
public sealed record Membresia(
    Guid Id,
    Guid OperadorId,
    Guid? CuentaComercialId,
    string Perfil);

/// <summary>③ Lo que la app manda para elegir operador (POST /api/auth/seleccionar-operador).</summary>
public sealed record SeleccionarOperadorRequest(
    Guid MembresiaId);