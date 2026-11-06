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
    // QR y los códigos de barras más usados en etiquetas de logística.
    private static readonly IDictionary<DecodeHintType, object> Pistas = new Dictionary<DecodeHintType, object>
    {
        [DecodeHintType.TRY_HARDER] = true,
        [DecodeHintType.POSSIBLE_FORMATS] = new List<BarcodeFormat>
        {
            BarcodeFormat.QR_CODE, BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.EAN_13
        }
    };

    // Tamaños (lado mayor, en píxeles) en los que se busca el código. Una foto sacada a una pantalla
    // o un poco movida suele leerse mejor achicada: al achicar se suavizan las rayas del monitor y el ruido.
    private static readonly int[] Tamanos = [0, 1200, 800, 500];

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
            using var original = await global::Android.Graphics.BitmapFactory.DecodeStreamAsync(imagen);
            if (original is null)
                return null;

            // Buscar el código lleva un momento: se hace fuera del hilo de la pantalla.
            return await Task.Run(() => Buscar(original));
        }
        catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException)
        {
            Debug.WriteLine($"Lector: sin cámara o sin permiso: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Una foto rara (o sin memoria para procesarla) no tiene que cerrar la app: cuenta como no leída.
            Debug.WriteLine($"Lector: no se pudo procesar la foto: {ex}");
            return null;
        }
    }

    /// <summary>Prueba la foto en varios tamaños y con dos formas de pasarla a blanco y negro.</summary>
    private static string? Buscar(global::Android.Graphics.Bitmap original)
    {
        var ladoMayor = Math.Max(original.Width, original.Height);

        foreach (var tamano in Tamanos)
        {
            if (tamano != 0 && tamano >= ladoMayor)
                continue;

            var bitmap = tamano == 0 ? original : Achicar(original, tamano);
            try
            {
                var fuente = Luminancia(bitmap);

                // HybridBinarizer: el umbral se calcula por zonas (sirve con luz despareja).
                // GlobalHistogramBinarizer: un solo umbral para toda la foto (sirve con fotos de pantalla).
                var texto = Decodificar(new BinaryBitmap(new HybridBinarizer(fuente)))
                         ?? Decodificar(new BinaryBitmap(new GlobalHistogramBinarizer(fuente)))
                         // Por si el código está en colores invertidos (claro sobre oscuro).
                         ?? Decodificar(new BinaryBitmap(new HybridBinarizer(fuente.invert())));

                if (texto is not null)
                    return texto;
            }
            finally
            {
                if (!ReferenceEquals(bitmap, original))
                    bitmap.Dispose();
            }
        }

        return null;
    }

    private static string? Decodificar(BinaryBitmap imagen)
    {
        try
        {
            return new MultiFormatReader().decode(imagen, Pistas)?.Text;
        }
        catch (ReaderException)
        {
            return null;
        }
    }

    private static global::Android.Graphics.Bitmap Achicar(global::Android.Graphics.Bitmap original, int ladoMayor)
    {
        var escala = (double)ladoMayor / Math.Max(original.Width, original.Height);
        var ancho = Math.Max(1, (int)(original.Width * escala));
        var alto = Math.Max(1, (int)(original.Height * escala));
        return global::Android.Graphics.Bitmap.CreateScaledBitmap(original, ancho, alto, filter: true)!;
    }

    /// <summary>Copia los píxeles de la foto a un arreglo que ZXing entiende (azul, verde, rojo, alfa).</summary>
    private static LuminanceSource Luminancia(global::Android.Graphics.Bitmap bitmap)
    {
        int ancho = bitmap.Width, alto = bitmap.Height;
        var pixeles = new int[ancho * alto];
        bitmap.GetPixels(pixeles, 0, ancho, 0, 0, ancho, alto);

        var bytes = new byte[pixeles.Length * 4];
        Buffer.BlockCopy(pixeles, 0, bytes, 0, bytes.Length);

        return new RGBLuminanceSource(bytes, ancho, alto, RGBLuminanceSource.BitmapFormat.BGRA32);
    }
}
