namespace UltimaMilla.Application.Envios.Queries.ConsultarSeguimientoPublico;

public sealed record MovimientoSeguimientoDto(
    string Estado,
    DateTimeOffset Fecha);


public sealed record SeguimientoPublicoDto(
    string Referencia,
    string Estado,
    DateTimeOffset FechaAlta,
    DateOnly? FechaEntregaComprometida,
    TimeOnly? HoraEntregaDesde,
    TimeOnly? HoraEntregaHasta,
    DateTimeOffset? FechaReprogramada,
    string? FranjaReprogramada,
    IReadOnlyList<MovimientoSeguimientoDto> Historial,
    int BultosTotales = 0,
    int BultosEntregados = 0,
    string Tipo = "Envio",
    string? OperadorNombre = null,
    string? OperadorColorPrimario = null,
    Guid? OperadorId = null);
