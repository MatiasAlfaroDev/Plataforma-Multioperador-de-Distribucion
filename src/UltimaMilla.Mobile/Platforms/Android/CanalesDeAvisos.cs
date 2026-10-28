using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace UltimaMilla.Mobile;

/// <summary>
/// Desde Android 8 cada notificación pertenece a un "canal" que el usuario puede silenciar en Ajustes.
/// La app usa uno solo, de importancia alta, para las reasignaciones urgentes.
/// </summary>
internal static class CanalesDeAvisos
{
    /// <summary>Mismo id que manda el servidor (FirebaseNotificadorPush) y que figura en AndroidManifest.xml.</summary>
    public const string Urgentes = "avisos_urgentes";

    /// <summary>Se llama al arrancar la app (MainApplication). Crearlo dos veces no hace nada.</summary>
    public static void Crear(Context contexto)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var canal = new NotificationChannel(Urgentes, "Avisos urgentes", NotificationImportance.High)
        {
            Description = "Reasignaciones de envíos y cambios en la ruta"
        };

        var administrador = (NotificationManager?)contexto.GetSystemService(Context.NotificationService);
        administrador?.CreateNotificationChannel(canal);
    }

    /// <summary>
    /// Muestra el aviso cuando la app está ABIERTA. Con la app cerrada o en segundo plano
    /// no hace falta: Android lo muestra solo con lo que manda Firebase.
    /// </summary>
    public static void Mostrar(Context contexto, string titulo, string cuerpo)
    {
        // Tocar la notificación abre la app.
        var abrirApp = contexto.PackageManager?.GetLaunchIntentForPackage(contexto.PackageName!);
        var alTocar = abrirApp is null
            ? null
            : PendingIntent.GetActivity(contexto, 0, abrirApp, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var aviso = new NotificationCompat.Builder(contexto, Urgentes)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle(titulo)
            .SetContentText(cuerpo)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetAutoCancel(true)
            .SetContentIntent(alTocar)
            .Build();

        NotificationManagerCompat.From(contexto).Notify(Random.Shared.Next(), aviso);
    }
}
