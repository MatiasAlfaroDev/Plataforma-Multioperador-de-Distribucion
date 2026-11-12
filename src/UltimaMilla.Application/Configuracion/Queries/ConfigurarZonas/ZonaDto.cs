
namespace UltimaMilla.Application.Configuracion.Queries.ListarZonas;

public sealed record ZonaDto(
    Guid Id,
    string Nombre,
    string Departamento,
    IReadOnlyList<LocalidadZonaDto> Localidades,
    IReadOnlyList<FranjaZonaDto> Franjas);

public sealed record LocalidadZonaDto(
    string Nombre,
    string? Sector);

public sealed record FranjaZonaDto(
    TimeOnly HoraDesde,
    TimeOnly HoraHasta);
