using System.Diagnostics;
using UltimaMilla.Mobile.Services;
using ZXing;
using ZXing.Common;

namespace UltimaMilla.Mobile;

/// <summary>
/// Lector de códigos en Android: la cámara del sistema saca la foto (MediaPicker, como en la
/// prueba de entrega) y ZXing.Net busca el código en la imagen. ZXing.Net es una biblioteca
/// 100 % .NET, así que no suma dependencias de AndroidX (no se repite el problema de Firebase).
/// </summary>
public sealed class LectorCodigosAndroid : ILectorCodigos
{
    public bool Disponible => true;

    public async Task<string?> LeerAsync(bool desdeGaleria)
    {
        try
        {
            var opciones = new MediaPickerOptions
            {
                Title = "Código del bulto",
                MaximumWidth = 1600,
                MaximumHeight = 1600
            };

            var foto = desdeGaleria
                ? await MediaPicker.Default.PickPhotoAsync(opciones)
                : await MediaPicker.Default.CapturePhotoAsync(opciones);

            if (foto is null)
                return null;

            await using var imagen = await foto.OpenReadAsync();
            using var bitmap = await global::Android.Graphics.BitmapFactory.DecodeStreamAsync(imagen);
            if (bitmap is null)
                return null;

            // Buscar el código lleva un momento: se hace fuera del hilo de la pantalla.
            return await Task.Run(() => Decodificar(bitmap));
        }
        catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException)
        {
            Debug.WriteLine($"Lector: sin cámara o sin permiso: {ex.Message}");
            return null;
        }
    }

    private static string? Decodificar(global::Android.Graphics.Bitmap bitmap)
    {
        // Se copian los píxeles de la foto a un arreglo que ZXing entiende (azul, verde, rojo, alfa).
        int ancho = bitmap.Width, alto = bitmap.Height;
        var pixeles = new int[ancho * alto];
        bitmap.GetPixels(pixeles, 0, ancho, 0, 0, ancho, alto);

        var bytes = new byte[pixeles.Length * 4];
        Buffer.BlockCopy(pixeles, 0, bytes, 0, bytes.Length);

        var lector = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                // QR y los códigos de barras más usados en etiquetas de logística.
                PossibleFormats = [BarcodeFormat.QR_CODE, BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.EAN_13]
            }
        };

        var resultado = lector.Decode(new RGBLuminanceSource(bytes, ancho, alto, RGBLuminanceSource.BitmapFormat.BGRA32));
        return resultado?.Text;
    }
}
