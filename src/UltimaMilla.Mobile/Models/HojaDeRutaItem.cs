namespace UltimaMilla.Mobile.Models;

/// <summary>Lo que devuelve GET /api/repartidor/hojas-de-ruta (mismos nombres que MiHojaDeRutaDto de Application).</summary>
public sealed record HojaDeRutaItem(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    string Repartidor,
    IReadOnlyList<ParadaItem> Paradas)
{
    /// <summary>Los bultos que se cargan en el depósito: los de los retiros todavía están en los domicilios.</summary>
    public int CantidadBultos => Paradas.Where(p => p.TipoOperacion != "Retiro").Sum(p => p.Bultos.Count);

    /// <summary>Ya salió: ningún envío sigue "Asignado a ruta". Los retiros siguen asignados hasta que se recogen.</summary>
    public bool YaDespachada =>
        Paradas.Any(p => p.TipoOperacion == "Entrega") &&
        Paradas.Where(p => p.TipoOperacion == "Entrega").All(p => p.Estado != "AsignadoARuta");

    public string Titulo => HoraSalida is { } hora ? $"Salida {hora:HH}:{hora:mm}" : "Sin hora de salida";

    public string Detalle =>
        $"{Paradas.Count} {(Paradas.Count == 1 ? "parada" : "paradas")}, {CantidadBultos} bultos"
        + (YaDespachada ? " · ya despachada" : string.Empty);
}

/// <param name="Bultos">Identificaciones de los bultos: las que se escanean al cargar el vehículo.</param>
/// <param name="TipoOperacion">RF 39: Entrega, DevolucionComercio, Retiro (se suben bultos) o EntregaRetiroComercio.</param>
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
    /// <summary>Cómo se muestra un retiro en la carga del vehículo (no tiene referencia del comercio).</summary>
    public string TituloRetiro =>
        RetiroId is Guid id ? $"Retiro {id.ToString()[..8].ToUpperInvariant()}" : ReferenciaExterna;

    public string DetalleRetiro =>
        $"{Direccion}, {Localidad} · {Bultos.Count} {(Bultos.Count == 1 ? "bulto" : "bultos")} a retirar en el domicilio";

    /// <summary>Lo que la app guarda como Id de la parada: el envío o el retiro.</summary>
    public Guid IdParada => RetiroId ?? EnvioId ?? Guid.Empty;
}
