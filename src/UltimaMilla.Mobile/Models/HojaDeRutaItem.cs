namespace UltimaMilla.Mobile.Models;

/// <summary>Lo que devuelve GET /api/repartidor/hojas-de-ruta (mismos nombres que MiHojaDeRutaDto de Application).</summary>
public sealed record HojaDeRutaItem(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    string Repartidor,
    IReadOnlyList<ParadaItem> Paradas)
{
    /// <summary>Las paradas de entrega: sus bultos son los que se cargan en el depósito.</summary>
    public IReadOnlyList<ParadaItem> Entregas => Paradas.Where(p => !p.EsRetiro).ToList();

    /// <summary>Solo los bultos a cargar en el depósito: los de los retiros todavía están en los domicilios (RF 39).</summary>
    public int CantidadBultos => Entregas.Sum(p => p.Bultos.Count);

    /// <summary>
    /// Ya salió: ningún envío sigue "Asignado a ruta". Los retiros no cuentan, porque siguen
    /// "Asignado a ruta" hasta que el repartidor pasa por el domicilio.
    /// </summary>
    public bool YaDespachada => Entregas.Count > 0 && Entregas.All(p => p.Estado != "AsignadoARuta");

    public int Retiros => Paradas.Count(p => p.EsRetiro);

    public string Titulo => HoraSalida is { } hora ? $"Salida {hora:HH}:{hora:mm}" : "Sin hora de salida";

    public string Detalle =>
        $"{Paradas.Count} {(Paradas.Count == 1 ? "parada" : "paradas")}, {CantidadBultos} bultos"
        + (Retiros > 0 ? $" · {Retiros} {(Retiros == 1 ? "retiro" : "retiros")}" : string.Empty)
        + (YaDespachada ? " · ya despachada" : string.Empty);
}

/// <param name="Bultos">Identificaciones de los bultos: en una entrega, las que se escanean al cargar el vehículo;
/// en un retiro, las que se escanean en el domicilio.</param>
/// <param name="EnvioId">El envío de una parada de entrega (null en un retiro).</param>
/// <param name="RetiroId">El retiro de una parada de retiro (RF 39; null en una entrega).</param>
/// <param name="TipoOperacion">"Entrega" o "Retiro".</param>
public sealed record ParadaItem(
    int Orden,
    Guid? EnvioId,
    string ReferenciaExterna,
    string Destinatario,
    string? TelefonoDestinatario,
    string Direccion,
    string Localidad,
    string Estado,
    IReadOnlyList<string> Bultos,
    IReadOnlyList<BultoItem>? DetalleBultos = null,
    Guid? RetiroId = null,
    string TipoOperacion = "Entrega")
{
    public bool EsRetiro => TipoOperacion == "Retiro";
}
