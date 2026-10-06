namespace UltimaMilla.Application.Envios;

/// <summary>Datos de un envío que salen de Application. No expone la entidad del dominio.</summary>
public sealed record EnvioDto(
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
