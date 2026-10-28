namespace UltimaMilla.Application.Abstractions;

/// <summary>Lo que se le muestra al repartidor en la notificación.</summary>
/// <param name="Datos">Pares clave/valor que la app recibe junto con el aviso (por ejemplo, el envío reasignado).</param>
public sealed record NotificacionPush(
    string Titulo,
    string Cuerpo,
    IReadOnlyDictionary<string, string>? Datos = null);

/// <summary>Cómo terminó el envío de un aviso a un teléfono.</summary>
public enum ResultadoPush
{
    /// <summary>Firebase lo aceptó y lo entrega aunque la app esté cerrada.</summary>
    Enviado,

    /// <summary>El token ya no existe (app desinstalada o datos borrados): hay que darlo de baja.</summary>
    TokenInvalido,

    /// <summary>El servidor no tiene la cuenta de servicio de Firebase.</summary>
    NoConfigurado,

    /// <summary>Otro error (red, cuota): el token se conserva.</summary>
    Fallido
}

/// <summary>
/// Manda notificaciones push. Application solo conoce esta interfaz; Infrastructure la
/// implementa con Firebase Cloud Messaging, así el dominio no depende de Firebase.
/// </summary>
public interface INotificadorPush
{
    Task<ResultadoPush> EnviarAsync(string token, NotificacionPush notificacion, CancellationToken ct);
}
