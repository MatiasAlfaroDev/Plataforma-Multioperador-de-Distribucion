
namespace UltimaMilla.Application.Envios.Queries.ObtenerDetalleEnvio;

public sealed record DetalleEnvioDto(
    Guid Id,
    string ReferenciaExterna,
    Guid OperadorId,
    Guid CuentaComercialId,
    string Estado,
    string Modalidad,
    string Destinatario,
    string Direccion,
    DateTimeOffset FechaAlta,
    decimal? TarifaCalculada,
    int IntentosRealizados,
    int BultosEntregados,
    int BultosPendientes,
    int BultosEnDevolucion,
    int BultosDevueltos,
    bool PuedeIniciarDevolucion,
    IReadOnlyList<DetalleBultoDto> Bultos,
    IReadOnlyList<DetalleEventoDto> Eventos
);

public sealed record DetalleBultoDto(
    Guid Id,
    string Identificacion,
    string Estado,
    decimal PesoKg,
    int AltoCm,
    int AnchoCm,
    int ProfundidadCm
);

public sealed record DetalleEventoDto(
    Guid Id,
    string EstadoAnterior,
    string EstadoNuevo,
    string Disparador,
    DateTimeOffset OcurridoEn,
    string Origen,
    string Responsable,
    string? Justificacion,
    string? ModalidadDevolucion,
    string? ReceptorDevolucion,
    string? ReferenciaConstanciaDevolucion
);
