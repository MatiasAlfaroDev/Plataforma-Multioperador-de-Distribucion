namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Lo que la app necesita del sistema de notificaciones del teléfono. En Android lo implementa
/// ServicioPushAndroid (Firebase); en las otras plataformas, ServicioPushNulo (no hace nada).
/// </summary>
public interface IServicioPush
{
    /// <summary>"Android" o "iOS": se manda a la API junto con el token.</summary>
    string Plataforma { get; }

    /// <summary>Android 13 o más pide permiso para mostrar notificaciones. Devuelve true si se puede.</summary>
    Task<bool> PedirPermisoAsync();

    /// <summary>El token de Firebase de esta instalación, o null si Firebase no está disponible.</summary>
    Task<string?> ObtenerTokenAsync();
}

/// <summary>Para Windows, iOS y Mac mientras no tengan push: no hay token, no se registra nada.</summary>
public sealed class ServicioPushNulo : IServicioPush
{
    public string Plataforma => DeviceInfo.Platform.ToString();

    public Task<bool> PedirPermisoAsync() => Task.FromResult(false);

    public Task<string?> ObtenerTokenAsync() => Task.FromResult<string?>(null);
}
