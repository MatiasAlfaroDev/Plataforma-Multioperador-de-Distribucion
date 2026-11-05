using System.Globalization;
using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Data;

/// <summary>
/// Cola de posiciones tomadas durante la jornada (tabla posiciones_pendientes). Igual que los
/// eventos: se guardan en el teléfono aunque no haya señal y salen de la cola cuando el servidor confirma.
/// </summary>
public sealed class PosicionesRepository(BaseDeDatosLocal db)
{
    public async Task AgregarAsync(PosicionPendiente posicion)
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT OR IGNORE INTO posiciones_pendientes (id, latitud, longitud, fecha_hora)
            VALUES ($id, $lat, $lon, $fecha)
            """;
        comando.Parameters.AddWithValue("$id", posicion.Id.ToString());
        comando.Parameters.AddWithValue("$lat", (double)posicion.Latitud);
        comando.Parameters.AddWithValue("$lon", (double)posicion.Longitud);
        comando.Parameters.AddWithValue("$fecha", posicion.RegistradaEn.ToString("o", CultureInfo.InvariantCulture));
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>Las más viejas primero, de a lotes.</summary>
    public async Task<IReadOnlyList<PosicionPendiente>> ListarLoteAsync(int cantidad)
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT id, latitud, longitud, fecha_hora FROM posiciones_pendientes ORDER BY fecha_hora LIMIT $n";
        comando.Parameters.AddWithValue("$n", cantidad);

        var lista = new List<PosicionPendiente>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            lista.Add(new PosicionPendiente(
                Guid.Parse(lector.GetString(0)),
                (decimal)lector.GetDouble(1),
                (decimal)lector.GetDouble(2),
                DateTimeOffset.Parse(lector.GetString(3), CultureInfo.InvariantCulture)));
        }
        return lista;
    }

    /// <summary>El servidor confirmó el lote: se borran del teléfono.</summary>
    public async Task ConfirmarAsync(IEnumerable<Guid> ids)
    {
        await using var conexion = await db.AbrirAsync();
        foreach (var id in ids)
        {
            var comando = conexion.CreateCommand();
            comando.CommandText = "DELETE FROM posiciones_pendientes WHERE id = $id";
            comando.Parameters.AddWithValue("$id", id.ToString());
            await comando.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> ContarAsync()
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM posiciones_pendientes";
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }
}
