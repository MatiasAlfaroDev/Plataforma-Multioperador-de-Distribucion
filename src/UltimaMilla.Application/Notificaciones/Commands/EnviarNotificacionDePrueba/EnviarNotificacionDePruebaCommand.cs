namespace UltimaMilla.Application.Notificaciones.Commands.EnviarNotificacionDePrueba;

/// <summary>Manda un aviso a los teléfonos del usuario de la sesión. Sirve para probar push con la app cerrada.</summary>
public sealed record EnviarNotificacionDePruebaCommand(string? Titulo, string? Cuerpo);

/// <param name="Dispositivos">Teléfonos registrados del usuario.</param>
/// <param name="Enviadas">Avisos que Firebase aceptó.</param>
/// <param name="TokensEliminados">Teléfonos dados de baja porque su token ya no existía.</param>
public sealed record ResultadoNotificacionDePrueba(int Dispositivos, int Enviadas, int TokensEliminados);
