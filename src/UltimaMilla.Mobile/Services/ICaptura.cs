namespace UltimaMilla.Mobile.Services;

/// <summary>Cámara y GPS del dispositivo (RF 20: la prueba registra posición y hora del dispositivo).</summary>
public interface ICaptura
{
    /// <summary>Saca una foto comprimida, la guarda en el teléfono y devuelve su referencia (nombre del archivo). Null si se cancela.</summary>
    Task<string?> SacarFotoAsync(Guid idEvento);

    /// <summary>La posición actual del dispositivo, o null si el GPS no está disponible.</summary>
    Task<(decimal Latitud, decimal Longitud)?> ObtenerUbicacionAsync();
}