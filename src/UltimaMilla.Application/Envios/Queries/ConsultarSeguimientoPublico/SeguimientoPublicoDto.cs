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
    // RF 35: avance de la entrega ("2 de 3 bultos") sin identificadores internos.
    int BultosTotales = 0,
    int BultosEntregados = 0,
    // RF 41: el mismo enlace muestra envíos y retiros a domicilio.
    string Tipo = "Envio");