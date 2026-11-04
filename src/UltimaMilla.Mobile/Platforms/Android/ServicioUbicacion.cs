using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile;

/// <summary>
/// Servicio en primer plano ("foreground service") que toma la posición durante la jornada (RF 23).
/// Android no lo cierra aunque la app pase a segundo plano o se apague la pantalla, a cambio de
/// mostrar un aviso fijo ("Jornada en curso"): así el repartidor siempre sabe que lo están siguiendo.
/// Cada posición se guarda en SQLite (funciona sin señal) y se sincroniza cuando hay Internet.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class ServicioUbicacion : Service, ILocationListener
{
    private const int IdAviso = 4201;

    /// <summary>Una posición cada 15 segundos o 20 metros, lo que pase después.</summary>
    private const long IntervaloMs = 15_000;
    private const float DistanciaMinimaMetros = 20;

    /// <summary>Cuántas posiciones juntar antes de intentar mandarlas (ahorra batería y datos).</summary>
    private const int LoteParaEnviar = 5;

    private LocationManager? _ubicacion;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CanalesDeAvisos.Crear(this);

        var abrirApp = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var alTocar = abrirApp is null
            ? null
            : PendingIntent.GetActivity(this, 0, abrirApp, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var aviso = new NotificationCompat.Builder(this, CanalesDeAvisos.Seguimiento)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle("Jornada en curso")
            .SetContentText("Tu operador ve tu ubicación hasta que termines la jornada.")
            .SetOngoing(true)
            .SetContentIntent(alTocar)
            .Build();

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(IdAviso, aviso, ForegroundService.TypeLocation);
        else
            StartForeground(IdAviso, aviso);

        _ubicacion ??= (LocationManager?)GetSystemService(LocationService);
        try
        {
            // GPS y, como respaldo, la red (más rápida en interiores). Llegan al mismo OnLocationChanged.
            _ubicacion?.RequestLocationUpdates(LocationManager.GpsProvider, IntervaloMs, DistanciaMinimaMetros, this, Looper.MainLooper);
            if (_ubicacion?.IsProviderEnabled(LocationManager.NetworkProvider) == true)
                _ubicacion.RequestLocationUpdates(LocationManager.NetworkProvider, IntervaloMs, DistanciaMinimaMetros, this, Looper.MainLooper);
        }
        catch (Java.Lang.SecurityException)
        {
            // Se quitó el permiso de ubicación: no hay nada que seguir.
            StopSelf();
        }

        // Sticky: si Android cierra el proceso por falta de memoria, vuelve a arrancar el servicio solo.
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _ubicacion?.RemoveUpdates(this);
        base.OnDestroy();
    }

    public void OnLocationChanged(Android.Locations.Location location)
    {
        var servicios = IPlatformApplication.Current?.Services;
        if (servicios is null)
            return;

        var posicion = new PosicionPendiente(
            Guid.NewGuid(),
            (decimal)location.Latitude,
            (decimal)location.Longitude,
            DateTimeOffset.FromUnixTimeMilliseconds(location.Time));

        _ = GuardarYEnviarAsync(servicios, posicion);
    }

    private static async Task GuardarYEnviarAsync(IServiceProvider servicios, PosicionPendiente posicion)
    {
        try
        {
            var posiciones = servicios.GetRequiredService<PosicionesRepository>();
            await posiciones.AgregarAsync(posicion);

            // Con señal y algunas posiciones juntas, se manda la cola (eventos y posiciones).
            var sincronizacion = servicios.GetRequiredService<SincronizacionService>();
            if (sincronizacion.HayInternet && await posiciones.ContarAsync() >= LoteParaEnviar)
                await sincronizacion.SincronizarAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Seguimiento: no se pudo guardar la posición: {ex.Message}");
        }
    }

    public void OnProviderDisabled(string provider) { }

    public void OnProviderEnabled(string provider) { }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
}
