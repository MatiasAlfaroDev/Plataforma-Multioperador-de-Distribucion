namespace UltimaMilla.Application.Retiros.Commands.RegistrarEventoRetiro;

public sealed record RegistrarEventoRetiroCommand(
    Guid RetiroId,
    Guid IdEvento,
    string Tipo,
    DateTimeOffset FechaHora,
    IReadOnlyList<string>? IdentificacionesBultos,
    string? ReferenciaFotografia,
    string? ReferenciaFirma,
    string? MotivoFallo);