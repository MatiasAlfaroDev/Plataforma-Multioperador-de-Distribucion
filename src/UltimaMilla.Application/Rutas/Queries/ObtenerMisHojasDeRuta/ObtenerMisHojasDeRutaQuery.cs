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
    IReadOnlyList<MiParadaDto> Paradas);

/// <param name="Bultos">Identificaciones que el repartidor escanea al cargar el vehículo (RF 19).</param>
public sealed record MiParadaDto(
    int Orden,
    Guid EnvioId,
    string ReferenciaExterna,
    string Destinatario,
    string? TelefonoDestinatario,
    string Direccion,
    string Localidad,
    string Estado,
    IReadOnlyList<string> Bultos);
