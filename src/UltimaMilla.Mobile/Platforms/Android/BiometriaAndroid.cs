using Android.Hardware.Biometrics;
using Android.OS;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile;

/// <summary>
/// Biometría con el BiometricPrompt que trae Android (API 30 o más). Es parte del sistema,
/// así que no suma paquetes ni dependencias de AndroidX.
/// </summary>
public sealed class BiometriaAndroid : IBiometria
{
    // Valores de BiometricManager.Authenticators: huella o rostro (BIOMETRIC_WEAK)
    // o, si no hay, el PIN / patrón / contraseña del teléfono (DEVICE_CREDENTIAL).
    private const int BiometriaDebil = 0x00FF;
    private const int CredencialDelDispositivo = 0x8000;

    public Task<ResultadoBiometria> ConfirmarAsync(string motivo)
    {
        // Android 10 o anterior: la combinación huella + PIN se configura distinto. No se implementó.
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            return Task.FromResult(ResultadoBiometria.Confirmado);

        var actividad = Platform.CurrentActivity;
        if (actividad is null)
            return Task.FromResult(ResultadoBiometria.Cancelado);

        var resultado = new TaskCompletionSource<ResultadoBiometria>();

        var dialogo = new BiometricPrompt.Builder(actividad)
            .SetTitle("Confirmá que sos vos")
            .SetSubtitle(motivo)
            .SetAllowedAuthenticators(BiometriaDebil | CredencialDelDispositivo)
            .Build();

        dialogo.Authenticate(new CancellationSignal(), actividad.MainExecutor!, new Respuesta(resultado));
        return resultado.Task;
    }

    /// <summary>Recibe lo que pasó en el diálogo del sistema y completa la tarea.</summary>
    private sealed class Respuesta(TaskCompletionSource<ResultadoBiometria> resultado) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result) =>
            resultado.TrySetResult(ResultadoBiometria.Confirmado);

        public override void OnAuthenticationError(BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString)
        {
            // Sin hardware, sin huellas cargadas o sin bloqueo de pantalla: no hay con qué validar.
            var codigo = (int)errorCode;
            var noConfigurada = codigo is 1 or 11 or 12 or 14 or 15;
            resultado.TrySetResult(noConfigurada ? ResultadoBiometria.NoConfigurada : ResultadoBiometria.Cancelado);
        }

        // Un dedo no reconocido no cierra el diálogo: Android deja reintentar, por eso acá no se hace nada.
        public override void OnAuthenticationFailed() { }
    }
}
