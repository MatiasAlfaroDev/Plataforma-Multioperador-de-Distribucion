namespace UltimaMilla.Application.Configuracion.Queries.ListarZonas;

public sealed record ZonaDto(
    Guid Id,
    string Nombre,
    string Departamento);