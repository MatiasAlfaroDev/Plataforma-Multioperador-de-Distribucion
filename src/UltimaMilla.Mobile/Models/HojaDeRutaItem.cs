namespace UltimaMilla.Mobile.Models;

/// <summary>
/// Lo que devuelve GET /api/repartidor/hojas-de-ruta (mismos nombres que MiHojaDeRutaDto de Application).
/// Solo llegan hojas que el despachador ya despachó desde el Backoffice (RF 17).
/// </summary>
public sealed record HojaDeRutaItem(
    Guid Id,
    DateOnly Fecha,
    TimeOnly? HoraSalida,
    string Repartidor,
    IReadOnlyList<ParadaItem> Paradas,
    string Estado = "Despachada")
{
    /// <summary>Las paradas que faltan hacer (las resueltas en la jornada también llegan, para verlas hasta rendir).</summary>
    public IReadOnlyList<ParadaItem> ParadasActivas => Paradas.Where(p => p.Activa).ToList();

    /// <summary>Los bultos que se cargan en el depósito: los de los retiros todavía están en los domicilios.</summary>
    public int CantidadBultos => ParadasActivas.Where(p => p.TipoOperacion != "Retiro").Sum(p => p.Bultos.Count);

    /// <summary>RF 22: el repartidor ya la rindió (falte o no el control del depósito); la jornada de esta hoja terminó.</summary>
    public bool EsRendida => Estado is "Rendida" or "PendienteDeControl";

    public string Titulo => HoraSalida is { } hora ? $"Salida {hora:HH}:{hora:mm}" : "Sin hora de salida";

    public string Detalle =>
        $"{ParadasActivas.Count} {(ParadasActivas.Count == 1 ? "parada" : "paradas")}, {CantidadBultos} bultos"
        + (EsRendida ? " · rendida" : string.Empty);
}

/// <param name="Bultos">Identificaciones de los bultos: las que se escanean al cargar el vehículo.</param>
/// <param name="TipoOperacion">RF 39: Entrega, DevolucionComercio, Retiro (se suben bultos) o EntregaRetiroComercio.</param>
/// <param name="Activa">False: ya se resolvió en la jornada; se muestra con su resultado hasta rendir.</param>
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
    string TipoOperacion = "Entrega",
    bool Activa = true)
{
    /// <summary>Cómo se muestra un retiro en la carga del vehículo (no tiene referencia del comercio).</summary>
    public string TituloRetiro =>
        RetiroId is Guid id ? $"Retiro {id.ToString()[..8].ToUpperInvariant()}" : ReferenciaExterna;

    public string DetalleRetiro =>
        $"{Direccion}, {Localidad} · {Bultos.Count} {(Bultos.Count == 1 ? "bulto" : "bultos")} a retirar en el domicilio";

    /// <summary>Lo que la app guarda como Id de la parada: el envío o el retiro.</summary>
    public Guid IdParada => RetiroId ?? EnvioId ?? Guid.Empty;
}
