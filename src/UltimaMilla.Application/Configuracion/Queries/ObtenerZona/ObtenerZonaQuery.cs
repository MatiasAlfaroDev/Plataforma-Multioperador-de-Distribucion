namespace UltimaMilla.Application.Configuracion.Queries.ObtenerZona;

/// <summary>Una zona con todo su detalle, para cargar el formulario de edición.</summary>
public sealed record ObtenerZonaQuery(Guid OperadorId, Guid ZonaId);

public sealed record CoberturaDto(string Localidad, string? Sector);

public sealed record FranjaDto(TimeOnly HoraDesde, TimeOnly HoraHasta);

public sealed record ZonaDetalleDto(
    Guid Id,
    string Nombre,
    string Departamento,
    int? MinutosTraslado,
    IReadOnlyList<CoberturaDto> Coberturas,
    IReadOnlyList<FranjaDto> Franjas);
