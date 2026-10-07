namespace UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;

public sealed record TarifaVigenteDto(
    Guid VersionTarifariaId,
    Guid CuadroTarifarioId,
    Guid ZonaCoberturaId,
    decimal PrecioBase,
    decimal PrecioPorKg,
    decimal PrecioPorMetroCubico,
    decimal RecargoUrgentePorcentaje,
    decimal BonificacionPorcentaje,
    DateTimeOffset VigenciaDesde);