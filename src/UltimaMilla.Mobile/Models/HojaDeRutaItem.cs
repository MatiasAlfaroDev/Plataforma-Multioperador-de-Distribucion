namespace UltimaMilla.Mobile.Models;

/// <summary>Lo que devuelve GET /api/repartidor/hojas-de-ruta (mismos nombres que MiHojaDeRutaDto de Application).</summary>
public sealed record HojaDeRutaItem(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    string Repartidor,
    IReadOnlyList<ParadaItem> Paradas);

/// <param name="Bultos">Identificaciones de los bultos: las que se escanean al cargar el vehículo.</param>
public sealed record ParadaItem(
    int Orden,
    Guid EnvioId,
    string ReferenciaExterna,
    string Destinatario,
    string? TelefonoDestinatario,
    string Direccion,
    string Localidad,
    string Estado,
    IReadOnlyList<string> Bultos);
