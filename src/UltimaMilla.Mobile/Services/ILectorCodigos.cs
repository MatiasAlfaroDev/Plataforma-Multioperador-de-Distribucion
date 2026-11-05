namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Lee el código de barras o QR de un bulto con la cámara (sección 6.11 y RF 19).
/// En Android lo implementa LectorCodigosAndroid; en las otras plataformas no hay lector.
/// </summary>
public interface ILectorCodigos
{
    bool Disponible { get; }

    /// <summary>
    /// Saca una foto (o elige una imagen de la galería) y devuelve el texto del código.
    /// Null si se canceló o no se encontró ningún código en la imagen.
    /// </summary>
    Task<string?> LeerAsync(bool desdeGaleria);
}

public sealed class LectorCodigosNulo : ILectorCodigos
{
    public bool Disponible => false;

    public Task<string?> LeerAsync(bool desdeGaleria) => Task.FromResult<string?>(null);
}
