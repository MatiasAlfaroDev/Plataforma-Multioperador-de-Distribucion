using Android.Content;
using AndroidX.Core.Content;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile;

/// <summary>Arranca y detiene el servicio de ubicación (ServicioUbicacion).</summary>
public sealed class SeguimientoAndroid : ISeguimiento
{
    private const string ClaveJornada = "jornada_activa";

    public bool Activo => Preferences.Default.Get(ClaveJornada, false);

    public async Task<bool> IniciarAsync()
    {
        // El servicio arranca con la app abierta, así que alcanza con el permiso "mientras se usa la app":
        // Android lo mantiene vivo con la pantalla apagada porque es un servicio en primer plano.
        var permiso = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (permiso != PermissionStatus.Granted)
            return false;

        // El aviso fijo de "Jornada en curso" es una notificación: en Android 13 o más necesita permiso.
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            await Permissions.RequestAsync<Permissions.PostNotifications>();

        var contexto = Platform.AppContext;
        ContextCompat.StartForegroundService(contexto, new Intent(contexto, typeof(ServicioUbicacion)));
        Preferences.Default.Set(ClaveJornada, true);
        return true;
    }

    public void Detener()
    {
        Preferences.Default.Set(ClaveJornada, false);
        var contexto = Platform.AppContext;
        contexto.StopService(new Intent(contexto, typeof(ServicioUbicacion)));
    }
}
