using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace UltimaMilla.Mobile.Data;

/// <summary>
/// Base SQLite del teléfono, CIFRADA con SQLCipher (RNF 6.11: almacenamiento local cifrado).
/// - La clave se genera al azar la primera vez y se guarda en SecureStorage, no en el código.
/// - Microsoft.Data.Sqlite manda la clave a SQLCipher a través de "Password" en la cadena de conexión.
/// - Sin la clave, el archivo repartidor.db3 es ilegible aunque alguien lo copie del teléfono.
/// </summary>
public sealed class BaseDeDatosLocal
{
    private readonly SemaphoreSlim _inicializacion = new(1, 1);
    private string? _cadenaDeConexion;

    /// <summary>Abre una conexión ya inicializada. Quien la pide la cierra con "await using".</summary>
    public async Task<SqliteConnection> AbrirAsync()
    {
        await InicializarAsync();
        var conexion = new SqliteConnection(_cadenaDeConexion);
        await conexion.OpenAsync();
        return conexion;
    }

    private async Task InicializarAsync()
    {
        if (_cadenaDeConexion is not null)
            return;

        await _inicializacion.WaitAsync();
        try
        {
            if (_cadenaDeConexion is not null)
                return;

            var cadena = new SqliteConnectionStringBuilder
            {
                DataSource = Constants.DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Password = await ObtenerClaveAsync()
            }.ToString();

            await using var conexion = new SqliteConnection(cadena);
            await conexion.OpenAsync();

            var comando = conexion.CreateCommand();
            comando.CommandText = """
                CREATE TABLE IF NOT EXISTS operadores (
                    id     TEXT PRIMARY KEY,
                    nombre TEXT NOT NULL,
                    color  TEXT
                );
                CREATE TABLE IF NOT EXISTS envios_ruta (
                    id              TEXT PRIMARY KEY,
                    operador_id     TEXT NOT NULL,
                    referencia      TEXT NOT NULL,
                    destinatario    TEXT NOT NULL,
                    direccion       TEXT NOT NULL,
                    comercio        TEXT NOT NULL,
                    modalidad       TEXT NOT NULL,
                    bultos          INTEGER NOT NULL,
                    estado_servidor TEXT NOT NULL,
                    resultado_local TEXT,
                    pendiente       INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS eventos_pendientes (
                    id           TEXT PRIMARY KEY,
                    envio_id     TEXT NOT NULL,
                    tipo         TEXT NOT NULL,
                    motivo       TEXT,
                    receptor     TEXT,
                    fecha_hora   TEXT NOT NULL,
                    intentos     INTEGER NOT NULL DEFAULT 0,
                    ultimo_error TEXT
                );
                """;
            await comando.ExecuteNonQueryAsync();

            _cadenaDeConexion = cadena;
        }
        finally
        {
            _inicializacion.Release();
        }
    }

    /// <summary>Lee la clave de SecureStorage; si no existe, crea una de 256 bits al azar.</summary>
    private static async Task<string> ObtenerClaveAsync()
    {
        var clave = await SecureStorage.Default.GetAsync(Constants.ClaveBaseLocalKey);
        if (string.IsNullOrEmpty(clave))
        {
            clave = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await SecureStorage.Default.SetAsync(Constants.ClaveBaseLocalKey, clave);
        }
        return clave;
    }
}
