using System.Diagnostics;
using Android.Gms.Extensions;
using Firebase.Messaging;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile;

/// <summary>Push en Android con Firebase Cloud Messaging.</summary>
public sealed class ServicioPushAndroid : IServicioPush
{
    public string Plataforma => "Android";

    public async Task<bool> PedirPermisoAsync()
    {
        // Antes de Android 13 las notificaciones no piden permiso.
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return true;

        var estado = await Permissions.RequestAsync<Permissions.PostNotifications>();
        return estado == PermissionStatus.Granted;
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        try
        {
            // GetToken() devuelve una tarea de Java; AsAsync la convierte en un Task de .NET.
            var token = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
            return token?.ToString();
        }
        catch (Exception ex)
        {
            // Pasa si falta google-services.json o el emulador no tiene Google APIs.
            Debug.WriteLine($"Push: Firebase no dio el token: {ex.Message}");
            return null;
        }
    }
}
