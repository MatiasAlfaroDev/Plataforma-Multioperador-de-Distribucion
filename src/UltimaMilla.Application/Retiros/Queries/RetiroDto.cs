namespace UltimaMilla.Application.Retiros.Queries;

public sealed record RetiroDto(
    Guid Id,
    Guid OperadorId,
    Guid CuentaComercialId,
    string Motivo,
    DireccionRetiroDto DomicilioRetiro,
    DireccionRetiroDto DomicilioDestino,
    DateOnly FechaSolicitada,
    TimeOnly HoraDesde,
    TimeOnly HoraHasta,
    string Estado,
    DateTimeOffset SolicitadoEn,
    Guid? HojaDeRutaId,
    DateTimeOffset? RetiradoEn,
    string? ReferenciaFotografia,
    string? ReferenciaFirma,
    decimal PesoTotalKg,
    decimal VolumenTotalMetrosCubicos,
    Guid? VersionTarifariaId,
    decimal? TarifaCalculada,
    IReadOnlyList<BultoRetiroDto> Bultos);

public sealed record DireccionRetiroDto(
    string Calle,
    string Numero,
    string Localidad,
    string Departamento);

public sealed record BultoRetiroDto(
    Guid Id,
    string Identificacion,
    decimal PesoKg,
    int AltoCm,
    int AnchoCm,
    int ProfundidadCm,
    decimal VolumenMetrosCubicos);