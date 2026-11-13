namespace UltimaMilla.Application.Retiros.Commands.RegistrarEventoRetiro;

public sealed record RegistrarEventoRetiroCommand(
    Guid RetiroId,
    Guid IdEvento,
    string Tipo,
    DateTimeOffset FechaHora,
    IReadOnlyList<string>? IdentificacionesBultos,
    string? ReferenciaFotografia,
    string? ReferenciaFirma,
    string? MotivoFallo,
    // La app puede mandar los Id de los bultos (los recibe en la hoja de ruta) en lugar de sus códigos.
    IReadOnlyList<Guid>? IdsBultos = null);