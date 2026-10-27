using System.Globalization;
using Microsoft.Data.Sqlite;
using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Data;

/// <summary>
/// La cola de lo que el repartidor registró sin conexión (tabla eventos_pendientes).
/// Un evento sale de la cola solo cuando el servidor confirma que lo recibió.
/// </summary>
public sealed class EventosPendientesRepository(BaseDeDatosLocal db)
{
    /// <summary>
    /// Registra el resultado de una entrega: agrega el evento a la cola y marca el envío
    /// en la ruta local. Las dos cosas en una sola transacción: o se guardan ambas o ninguna.
    /// </summary>
    public async Task RegistrarAsync(EventoPendiente evento)
    {
        await using var conexion = await db.AbrirAsync();
        await using var tx = (SqliteTransaction)await conexion.BeginTransactionAsync();

        var insertar = conexion.CreateCommand();
        insertar.Transaction = tx;
        insertar.CommandText = """
            INSERT INTO eventos_pendientes (id, envio_id, tipo, motivo, receptor, fecha_hora, latitud, longitud, foto)
            VALUES ($id, $envio, $tipo, $motivo, $receptor, $fecha, $lat, $lon, $foto)
            """;
        insertar.Parameters.AddWithValue("$id", evento.Id.ToString());
        insertar.Parameters.AddWithValue("$envio", evento.EnvioId.ToString());
        insertar.Parameters.AddWithValue("$tipo", evento.Tipo);
        insertar.Parameters.AddWithValue("$motivo", (object?)evento.Motivo ?? DBNull.Value);
        insertar.Parameters.AddWithValue("$receptor", (object?)evento.Receptor ?? DBNull.Value);
        insertar.Parameters.AddWithValue("$fecha", evento.FechaHora.ToString("o", CultureInfo.InvariantCulture));
        insertar.Parameters.AddWithValue("$lat", (object?)evento.Latitud ?? DBNull.Value);
        insertar.Parameters.AddWithValue("$lon", (object?)evento.Longitud ?? DBNull.Value);
        insertar.Parameters.AddWithValue("$foto", (object?)evento.ReferenciaFoto ?? DBNull.Value);
        await insertar.ExecuteNonQueryAsync();

        var marcar = conexion.CreateCommand();
        marcar.Transaction = tx;
        marcar.CommandText = "UPDATE envios_ruta SET resultado_local = $tipo, pendiente = 1 WHERE id = $envio";
        marcar.Parameters.AddWithValue("$tipo", evento.Tipo);
        marcar.Parameters.AddWithValue("$envio", evento.EnvioId.ToString());
        await marcar.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    /// <summary>Pendientes en el orden en que ocurrieron (importa: un envío no puede entregarse antes de salir).</summary>
    public async Task<IReadOnlyList<EventoPendiente>> ListarAsync()
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT id, envio_id, tipo, motivo, receptor, fecha_hora, intentos, ultimo_error, latitud, longitud, foto
            FROM eventos_pendientes
            ORDER BY fecha_hora
            """;

        var lista = new List<EventoPendiente>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            lista.Add(new EventoPendiente
            {
                Id = Guid.Parse(lector.GetString(0)),
                EnvioId = Guid.Parse(lector.GetString(1)),
                Tipo = lector.GetString(2),
                Motivo = lector.IsDBNull(3) ? null : lector.GetString(3),
                Receptor = lector.IsDBNull(4) ? null : lector.GetString(4),
                FechaHora = DateTimeOffset.Parse(lector.GetString(5), CultureInfo.InvariantCulture),
                Intentos = lector.GetInt32(6),
                UltimoError = lector.IsDBNull(7) ? null : lector.GetString(7),
                Latitud = lector.IsDBNull(8) ? null : (decimal)lector.GetDouble(8),
                Longitud = lector.IsDBNull(9) ? null : (decimal)lector.GetDouble(9),
                ReferenciaFoto = lector.IsDBNull(10) ? null : lector.GetString(10)
            });
        }
        return lista;
    }

    public async Task<int> ContarAsync()
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM eventos_pendientes";
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    /// <summary>El servidor confirmó: el evento sale de la cola y, si era el último de ese envío, deja de estar pendiente.</summary>
    public async Task ConfirmarAsync(EventoPendiente evento)
    {
        await using var conexion = await db.AbrirAsync();
        await using var tx = (SqliteTransaction)await conexion.BeginTransactionAsync();

        var borrar = conexion.CreateCommand();
        borrar.Transaction = tx;
        borrar.CommandText = "DELETE FROM eventos_pendientes WHERE id = $id";
        borrar.Parameters.AddWithValue("$id", evento.Id.ToString());
        await borrar.ExecuteNonQueryAsync();

        var actualizar = conexion.CreateCommand();
        actualizar.Transaction = tx;
        actualizar.CommandText = """
            UPDATE envios_ruta SET pendiente = 0
            WHERE id = $envio AND NOT EXISTS (SELECT 1 FROM eventos_pendientes WHERE envio_id = $envio)
            """;
        actualizar.Parameters.AddWithValue("$envio", evento.EnvioId.ToString());
        await actualizar.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    /// <summary>El servidor rechazó el evento: queda en la cola con el motivo, para revisarlo.</summary>
    public async Task RegistrarFalloAsync(EventoPendiente evento, string error)
    {
        await using var conexion = await db.AbrirAsync();
        var comando = conexion.CreateCommand();
        comando.CommandText = "UPDATE eventos_pendientes SET intentos = intentos + 1, ultimo_error = $error WHERE id = $id";
        comando.Parameters.AddWithValue("$error", error);
        comando.Parameters.AddWithValue("$id", evento.Id.ToString());
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// El repartidor acepta lo que dice el servidor: se borra lo registrado para ese envío
    /// y el envío vuelve a "Por entregar" en el teléfono. Es la última palabra del ADR de conflictos.
    /// </summary>
    public async Task DescartarAsync(Guid envioId)
    {
        await using var conexion = await db.AbrirAsync();
        await using var tx = (SqliteTransaction)await conexion.BeginTransactionAsync();

        var borrar = conexion.CreateCommand();
        borrar.Transaction = tx;
        borrar.CommandText = "DELETE FROM eventos_pendientes WHERE envio_id = $envio";
        borrar.Parameters.AddWithValue("$envio", envioId.ToString());
        await borrar.ExecuteNonQueryAsync();

        var reiniciar = conexion.CreateCommand();
        reiniciar.Transaction = tx;
        reiniciar.CommandText = "UPDATE envios_ruta SET resultado_local = NULL, pendiente = 0 WHERE id = $envio";
        reiniciar.Parameters.AddWithValue("$envio", envioId.ToString());
        await reiniciar.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }
}