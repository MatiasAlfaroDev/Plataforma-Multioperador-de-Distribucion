namespace UltimaMilla.Mobile.Data;

/// <summary>Constantes de la app, como Constants.cs de la demo BeatScan.</summary>
public static class Constants
{
    /// <summary>
    /// Dirección de la API (Traefik del docker compose, puerto 8000).
    /// - Emulador Android: 10.0.2.2 es la PC donde corre docker compose.
    /// - App de Windows: localhost.
    /// - Celular físico: reemplazar por la IP de la PC en la red Wi-Fi (por ejemplo http://192.168.1.20:8000/).
    /// </summary>
    public static string BaseApiUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:8000/"
            : "http://localhost:8000/";

    /// <summary>Archivo de la base local, en la carpeta privada de la app.</summary>
    public const string DatabaseFilename = "repartidor.db3";

    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);

    /// <summary>
    /// Nombre con el que se guarda la clave de cifrado en SecureStorage
    /// (Keystore en Android, Keychain en iOS, almacén protegido en Windows).
    /// La clave nunca está escrita en el código.
    /// </summary>
    public const string ClaveBaseLocalKey = "clave_base_local";
}
