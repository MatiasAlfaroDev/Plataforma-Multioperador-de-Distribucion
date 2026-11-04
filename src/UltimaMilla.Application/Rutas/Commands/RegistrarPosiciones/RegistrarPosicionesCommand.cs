namespace UltimaMilla.Application.Rutas.Commands.RegistrarPosiciones;

/// <summary>Un lote de posiciones que la app juntó (muchas veces sin conexión) y ahora sincroniza.</summary>
public sealed record RegistrarPosicionesCommand(IReadOnlyList<PosicionCommand> Posiciones);

/// <param name="Id">Lo generó el teléfono al tomar la posición: hace idempotente el reenvío.</param>
public sealed record PosicionCommand(Guid Id, decimal Latitud, decimal Longitud, DateTimeOffset RegistradaEn);
