
namespace UltimaMilla.Domain.Envios;

public sealed class ResultadoBultoIntento
{
    public Guid Id { get; private set; }
    public Guid IntentoEntregaId { get; private set; }
    public Guid BultoId { get; private set; }

    public bool Entregado { get; private set; }

    public Guid? MotivoNoEntregaId { get; private set; }

    public string? ReferenciaEvidenciaFallo { get; private set; }

    private ResultadoBultoIntento() { }

    internal static ResultadoBultoIntento CrearEntregado(
        Guid intentoEntregaId,
        Guid bultoId)
    {
        ValidarIds(intentoEntregaId, bultoId);

        return new ResultadoBultoIntento
        {
            Id = Guid.NewGuid(),
            IntentoEntregaId = intentoEntregaId,
            BultoId = bultoId,
            Entregado = true
        };
    }

    internal static ResultadoBultoIntento CrearNoEntregado(
        Guid intentoEntregaId,
        Guid bultoId,
        Guid motivoNoEntregaId,
        string evidencia)
    {
        ValidarIds(intentoEntregaId, bultoId);

        if (motivoNoEntregaId == Guid.Empty)
            throw new ArgumentException(
                "El motivo de no entrega es obligatorio.");

        if (string.IsNullOrWhiteSpace(evidencia))
            throw new ArgumentException(
                "La evidencia del fallo es obligatoria.");

        return new ResultadoBultoIntento
        {
            Id = Guid.NewGuid(),
            IntentoEntregaId = intentoEntregaId,
            BultoId = bultoId,
            Entregado = false,
            MotivoNoEntregaId = motivoNoEntregaId,
            ReferenciaEvidenciaFallo = evidencia.Trim()
        };
    }

    private static void ValidarIds(
        Guid intentoEntregaId,
        Guid bultoId)
    {
        if (intentoEntregaId == Guid.Empty ||
            bultoId == Guid.Empty)
        {
            throw new ArgumentException(
                "El intento y el bulto son obligatorios.");
        }
    }
}
