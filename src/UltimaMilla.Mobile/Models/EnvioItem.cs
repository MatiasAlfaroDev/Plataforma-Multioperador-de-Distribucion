namespace UltimaMilla.Mobile.Models;

/// <summary>Lo que devuelve GET /api/envios (mismos nombres que EnvioDto de Application).</summary>
public sealed record EnvioItem(
    Guid Id,
    string ReferenciaExterna,
    Guid OperadorId,
    string OperadorNombre,
    string ComercioNombre,
    string Modalidad,
    string Estado,
    string DestinatarioNombre,
    string Direccion,
    int CantidadBultos,
    decimal PesoTotalKg,
    DateTimeOffset FechaAlta,
    // Los bultos pendientes con su Id. Vienen en la hoja de ruta del repartidor; con otros
    // perfiles (GET /api/envios) no vienen y el resultado se registra para el envío completo.
    IReadOnlyList<BultoItem>? Bultos = null,
    // "Entrega" o "Retiro" (RF 39). En un retiro, Id es el del retiro y se guardan las
    // identificaciones de los bultos a retirar, para escanearlas en el domicilio sin conexión.
    string TipoOperacion = "Entrega",
    IReadOnlyList<string>? IdentificacionesRetiro = null);
