
namespace UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;

public sealed record BultoNoEntregadoMovil(
    Guid BultoId,
    string Motivo,
    string Evidencia);

public sealed record RegistrarEventoMovilCommand(
    Guid EnvioId,
    Guid IdEvento,
    string Tipo,
    string? Motivo,
    string? Receptor,
    DateTimeOffset FechaHora,
    string? DocumentoReceptor = null,
    string? ReferenciaFirma = null,
    string? ReferenciaFotografia = null,
    decimal? Latitud = null,
    decimal? Longitud = null,
    string? EvidenciaFallo = null,
    IReadOnlyCollection<Guid>? BultosEntregados = null,
    IReadOnlyCollection<BultoNoEntregadoMovil>? BultosNoEntregados = null);
