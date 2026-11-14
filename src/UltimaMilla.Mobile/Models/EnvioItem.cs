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
    // Si la parada ya se resolvió en el servidor (Entregado, EntregadoParcialmente, NoEntregado, Retirado o
    // NoRetirado), su resultado: el teléfono la muestra resuelta hasta rendir, aunque se haya reinstalado la app.
    string? ResultadoServidor = null);
