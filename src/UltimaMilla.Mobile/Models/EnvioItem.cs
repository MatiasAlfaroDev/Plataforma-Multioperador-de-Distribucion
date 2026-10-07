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
    DateTimeOffset FechaAlta);
