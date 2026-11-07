using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Retiros;

public sealed class EventoRetiroMovil : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public Guid RetiroId { get; private set; }

    public string Tipo { get; private set; } = string.Empty;
    public DateTimeOffset OcurridoEn { get; private set; }
    public DateTimeOffset RecibidoEn { get; private set; }

    public string? MotivoFallo { get; private set; }
    public string? ReferenciaFotografia { get; private set; }
    public string? ReferenciaFirma { get; private set; }

    public string[] IdentificacionesBultos { get; private set; } = [];

    private EventoRetiroMovil() { }

    public static EventoRetiroMovil Crear(
        Guid idEvento,
        Retiro retiro,
        string tipo,
        DateTimeOffset ocurridoEn,
        DateTimeOffset recibidoEn,
        IReadOnlyCollection<string>? identificacionesBultos,
        string? referenciaFotografia,
        string? referenciaFirma,
        string? motivoFallo)
    {
        ArgumentNullException.ThrowIfNull(retiro);

        if (idEvento == Guid.Empty)
            throw new ArgumentException(
                "El identificador del evento es obligatorio.");

        if (tipo is not ("Retirado" or "NoRetirado"))
            throw new ArgumentException(
                "El tipo de evento no es válido.");

        if (ocurridoEn > recibidoEn.AddMinutes(5))
            throw new ArgumentException(
                "La fecha del evento no puede estar en el futuro.");

        if (tipo == "Retirado")
        {
            if (string.IsNullOrWhiteSpace(referenciaFotografia) ||
                string.IsNullOrWhiteSpace(referenciaFirma))
                throw new ArgumentException(
                    "El retiro requiere fotografía y firma.");

            if (identificacionesBultos is null ||
                identificacionesBultos.Count == 0)
                throw new ArgumentException(
                    "Debe identificar los bultos retirados.");
        }
        else if (string.IsNullOrWhiteSpace(motivoFallo))
        {
            throw new ArgumentException(
                "Debe indicar el motivo del retiro fallido.");
        }

        return new EventoRetiroMovil
        {
            Id = idEvento,
            OperadorId = retiro.OperadorId,
            RetiroId = retiro.Id,
            Tipo = tipo,
            OcurridoEn = ocurridoEn.ToUniversalTime(),
            RecibidoEn = recibidoEn.ToUniversalTime(),
            IdentificacionesBultos =
                identificacionesBultos?.ToArray() ?? [],
            ReferenciaFotografia = referenciaFotografia?.Trim(),
            ReferenciaFirma = referenciaFirma?.Trim(),
            MotivoFallo = motivoFallo?.Trim()
        };
    }
}