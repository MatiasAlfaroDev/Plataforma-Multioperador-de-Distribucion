namespace UltimaMilla.Mobile.Services;

public enum ResultadoBiometria
{
    /// <summary>La persona confirmó con huella, rostro o el PIN del teléfono.</summary>
    Confirmado,

    /// <summary>Canceló o no la reconoció.</summary>
    Cancelado,

    /// <summary>El teléfono no tiene bloqueo de pantalla ni huella configurados: no se puede validar.</summary>
    NoConfigurada
}

/// <summary>
/// Validación biométrica para operaciones sensibles (6.11): despachar la ruta, descartar un
/// registro rechazado y entrar sin conexión (sin contraseña). En Android usa el BiometricPrompt
/// del sistema; si el teléfono no tiene huella, acepta el PIN o patrón de desbloqueo.
/// </summary>
public interface IBiometria
{
    Task<ResultadoBiometria> ConfirmarAsync(string motivo);
}

/// <summary>Plataformas sin biometría implementada todavía: no bloquea la operación.</summary>
public sealed class BiometriaNula : IBiometria
{
    public Task<ResultadoBiometria> ConfirmarAsync(string motivo) => Task.FromResult(ResultadoBiometria.Confirmado);
}
