
namespace UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;

/// <summary>
/// Evento generado por el móvil y sincronizado posteriormente.
/// IdEvento identifica el evento para evitar procesarlo dos veces.
/// FechaHora indica cuándo ocurrió realmente.
/// </summary>
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
    string? EvidenciaFallo = null);
