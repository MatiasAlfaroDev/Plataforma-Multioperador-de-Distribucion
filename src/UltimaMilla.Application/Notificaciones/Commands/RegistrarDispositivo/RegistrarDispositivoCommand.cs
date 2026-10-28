namespace UltimaMilla.Application.Notificaciones.Commands.RegistrarDispositivo;

/// <summary>
/// La app avisa "este teléfono puede recibir notificaciones". El usuario y el operador
/// no viajan en el cuerpo: salen de la cookie de sesión.
/// </summary>
public sealed record RegistrarDispositivoCommand(string Token, string Plataforma);
