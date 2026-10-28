namespace UltimaMilla.Mobile.Models;

/// <summary>Lo que devuelve GET /api/repartidor/hojas-de-ruta (mismos nombres que MiHojaDeRutaDto de Application).</summary>
public sealed record HojaDeRutaItem(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    string Repartidor,
    IReadOnlyList<ParadaItem> Paradas)
{
    public int CantidadBultos => Paradas.Sum(p => p.Bultos.Count);

    /// <summary>Ya salió: ningún envío sigue "Asignado a ruta".</summary>
    public bool YaDespachada => Paradas.Count > 0 && Paradas.All(p => p.Estado != "AsignadoARuta");

    public string Titulo => HoraSalida is { } hora ? $"Salida {hora:HH}:{hora:mm}" : "Sin hora de salida";

    public string Detalle =>
        $"{Paradas.Count} {(Paradas.Count == 1 ? "parada" : "paradas")} · {CantidadBultos} bultos"
        + (YaDespachada ? " · ya despachada" : string.Empty);
}

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
