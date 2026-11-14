namespace UltimaMilla.Application.Rutas.Queries.ObtenerMisHojasDeRuta;

/// <summary>
/// Las hojas de ruta del repartidor que inició sesión en la app (RF 17 y 18).
/// El repartidor y el operador salen de la cookie; solo viaja la fecha del teléfono.
/// </summary>
public sealed record ObtenerMisHojasDeRutaQuery(DateOnly Fecha);

public sealed record MiHojaDeRutaDto(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    string Repartidor,
    IReadOnlyList<MiParadaDto> Paradas,
    string Estado = "Despachada");

/// <param name="Bultos">Identificaciones de los bultos pendientes, que el repartidor escanea al cargar el vehículo (RF 19).</param>
/// <param name="DetalleBultos">Los mismos bultos con su Id: la app los usa para informar una entrega parcial (RF 32).</param>
/// <param name="Activa">False: la parada ya se resolvió durante la jornada (entregada, parcial, no entregada, retirada
/// o no retirada). Sigue llegando a la app hasta rendir, para que el repartidor vea su día completo.</param>
public sealed record MiParadaDto(
    int Orden,
    Guid? EnvioId,
    string ReferenciaExterna,
    string Destinatario,
    string? TelefonoDestinatario,
    string Direccion,
    string Localidad,
    string Estado,
    IReadOnlyList<string> Bultos,
    IReadOnlyList<MiBultoDto>? DetalleBultos = null,
    Guid? RetiroId = null,
    string TipoOperacion = "Entrega",
    bool Activa = true);

public sealed record MiBultoDto(Guid Id, string Identificacion);
