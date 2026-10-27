namespace UltimaMilla.Mobile.Services;

/// <summary>Implementación con MediaPicker (cámara) y Geolocation (GPS) de .NET MAUI.</summary>
public sealed class Captura : ICaptura
{
    public async Task<string?> SacarFotoAsync(Guid idEvento)
    {
        // Compresión (RNF 6.11): la foto se achica y se comprime al sacarla.
        var opciones = new MediaPickerOptions
        {
            Title = "Prueba de entrega",
            MaximumWidth = 1280,
            MaximumHeight = 1280,
            CompressionQuality = 70
        };

        FileResult? foto;
        try
        {
            // En el celular abre la cámara. En Windows, si no hay cámara, deja elegir una imagen.
            foto = MediaPicker.Default.IsCaptureSupported
                ? await MediaPicker.Default.CapturePhotoAsync(opciones)
                : await MediaPicker.Default.PickPhotoAsync(opciones);
        }
        catch (FeatureNotSupportedException)
        {
            foto = await MediaPicker.Default.PickPhotoAsync(opciones);
        }
        catch (PermissionException)
        {
            return null;
        }

        if (foto is null)
            return null;

        // Se guarda en la carpeta privada de la app. Al servidor viaja solo la referencia.
        var carpeta = Path.Combine(FileSystem.AppDataDirectory, "fotos");
        Directory.CreateDirectory(carpeta);
        var nombre = $"{idEvento}.jpg";

        await using var origen = await foto.OpenReadAsync();
        await using var destino = File.Create(Path.Combine(carpeta, nombre));
        await origen.CopyToAsync(destino);

        return nombre;
    }

    public async Task<(decimal Latitud, decimal Longitud)?> ObtenerUbicacionAsync()
    {
        try
        {
            // El GPS funciona sin Internet: sirve aunque el repartidor esté sin señal.
            var ubicacion = await Geolocation.Default.GetLocationAsync(
                                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)))
                            ?? await Geolocation.Default.GetLastKnownLocationAsync();

            return ubicacion is null ? null : ((decimal)ubicacion.Latitude, (decimal)ubicacion.Longitude);
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            return null;
        }
    }
}