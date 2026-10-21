using Microsoft.Data.Sqlite;
using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Data;

/// <summary>
/// Guarda en el teléfono lo que se descarga de la API (operadores y hoja de ruta)
/// para poder mostrarlo sin conexión. Mismo estilo que los repositorios de la demo BeatScan.
/// </summary>
public sealed class RutaRepository(BaseDeDatosLocal db)
{
    public async Task GuardarOperadoresAsync(IEnumerable<OperadorItem> operadores)
    {
        await using var conexion = await db.AbrirAsync();
        await using var tx = (SqliteTransaction)await conexion.BeginTransactionAsync();

        var borrar = conexion.CreateCommand();
        borrar.Transaction = tx;
        borrar.CommandText = "DELETE FROM operadores";
        await borrar.ExecuteNonQueryAsync();

        foreach (var o in operadores)
        {
            var insertar = conexion.CreateCommand();
            insertar.Transaction = tx;
            insertar.CommandText = "INSERT INTO operadores (id, nombre, color) VALUES ($id, $nombre, $color)";
            insertar.Parameters.AddWithValue("$id", o.Id.ToString());
            insertar.Parameters.AddWithValue("$nombre", o.Nombre);
            insertar.Parameters.AddWithValue("$color", o.ColorPrimario ?? string.Empty);
            await insertar.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task<IReadOnlyList<OperadorItem>> ListarOperadoresAsync()
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT id, nombre, color FROM operadores ORDER BY nombre";

        var lista = new List<OperadorItem>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            lista.Add(new OperadorItem(Guid.Parse(lector.GetString(0)), lector.GetString(1), lector.IsDBNull(2) ? string.Empty : lector.GetString(2)));
        return lista;
    }

    /// <summary>
    /// Reemplaza la ruta guardada del operador por la recién descargada, SIN perder lo que
    /// el repartidor registró: el resultado local (Entregado / No entregado) se conserva,
    /// esté o no sincronizado, y solo se actualizan los datos que vienen del servidor.
    /// Se borran los envíos que ya no están en la ruta, salvo los que faltan sincronizar.
    /// </summary>
    public async Task GuardarRutaAsync(Guid operadorId, IEnumerable<EnvioItem> envios)
    {
        await using var conexion = await db.AbrirAsync();
        await using var tx = (SqliteTransaction)await conexion.BeginTransactionAsync();

        var ids = new List<string>();
        foreach (var e in envios)
        {
            ids.Add(e.Id.ToString());
            var upsert = conexion.CreateCommand();
            upsert.Transaction = tx;
            upsert.CommandText = """
                INSERT INTO envios_ruta (id, operador_id, referencia, destinatario, direccion, comercio, modalidad, bultos, estado_servidor)
                VALUES ($id, $op, $ref, $dest, $dir, $com, $mod, $bultos, $estado)
                ON CONFLICT(id) DO UPDATE SET
                    referencia = excluded.referencia,
                    destinatario = excluded.destinatario,
                    direccion = excluded.direccion,
                    comercio = excluded.comercio,
                    modalidad = excluded.modalidad,
                    bultos = excluded.bultos,
                    estado_servidor = excluded.estado_servidor
                """;
            upsert.Parameters.AddWithValue("$id", e.Id.ToString());
            upsert.Parameters.AddWithValue("$op", operadorId.ToString());
            upsert.Parameters.AddWithValue("$ref", e.ReferenciaExterna);
            upsert.Parameters.AddWithValue("$dest", e.DestinatarioNombre);
            upsert.Parameters.AddWithValue("$dir", e.Direccion);
            upsert.Parameters.AddWithValue("$com", e.ComercioNombre);
            upsert.Parameters.AddWithValue("$mod", e.Modalidad);
            upsert.Parameters.AddWithValue("$bultos", e.CantidadBultos);
            upsert.Parameters.AddWithValue("$estado", e.Estado);
            await upsert.ExecuteNonQueryAsync();
        }

        // Envíos que ya no vienen en la ruta (por ejemplo, reasignados a otro repartidor):
        // se borran del teléfono, salvo los que tienen algo pendiente de mandar al servidor.
        var borrar = conexion.CreateCommand();
        borrar.Transaction = tx;
        var parametros = new List<string>();
        for (var i = 0; i < ids.Count; i++)
        {
            parametros.Add($"$id{i}");
            borrar.Parameters.AddWithValue($"$id{i}", ids[i]);
        }
        borrar.CommandText = ids.Count == 0
            ? "DELETE FROM envios_ruta WHERE operador_id = $op AND pendiente = 0"
            : $"DELETE FROM envios_ruta WHERE operador_id = $op AND pendiente = 0 AND id NOT IN ({string.Join(", ", parametros)})";
        borrar.Parameters.AddWithValue("$op", operadorId.ToString());
        await borrar.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    public async Task<IReadOnlyList<EnvioRuta>> ListarRutaAsync(Guid operadorId)
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT id, operador_id, referencia, destinatario, direccion, comercio, modalidad, bultos,
                   estado_servidor, resultado_local, pendiente
            FROM envios_ruta
            WHERE operador_id = $op
            ORDER BY (resultado_local IS NOT NULL), referencia  -- primero lo que falta entregar
            """;
        comando.Parameters.AddWithValue("$op", operadorId.ToString());

        var lista = new List<EnvioRuta>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            lista.Add(new EnvioRuta
            {
                Id = Guid.Parse(lector.GetString(0)),
                OperadorId = Guid.Parse(lector.GetString(1)),
                ReferenciaExterna = lector.GetString(2),
                DestinatarioNombre = lector.GetString(3),
                Direccion = lector.GetString(4),
                ComercioNombre = lector.GetString(5),
                Modalidad = lector.GetString(6),
                CantidadBultos = lector.GetInt32(7),
                EstadoServidor = lector.GetString(8),
                ResultadoLocal = lector.IsDBNull(9) ? null : lector.GetString(9),
                PendienteDeSincronizar = lector.GetInt32(10) == 1
            });
        }
        return lista;
    }
}
