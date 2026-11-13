using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Envios;

public enum TipoDiscrepanciaRecepcion
{
    /// <summary>El comercio declaró el bulto, pero no se escaneó al recibir el envío.</summary>
    BultoFaltante,

    /// <summary>Se escaneó un bulto que el comercio no declaró para ese envío.</summary>
    BultoNoDeclarado
}

/// <summary>
/// Diferencia entre lo que el comercio declaró y lo que se escaneó al recibir el envío en el
/// depósito (RF 10). Queda registrada aunque la recepción no se complete.
/// </summary>
public class DiscrepanciaRecepcion : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public Guid EnvioId { get; private set; }
    public TipoDiscrepanciaRecepcion Tipo { get; private set; }

    /// <summary>Identificación del bulto faltante o no declarado.</summary>
    public string Identificacion { get; private set; } = string.Empty;

    public string Responsable { get; private set; } = string.Empty;
    public DateTimeOffset RegistradaEn { get; private set; }

    // Constructor vacío que necesita EF Core para materializar desde la base.
    private DiscrepanciaRecepcion() { }

    public static DiscrepanciaRecepcion Registrar(
        Envio envio,
        TipoDiscrepanciaRecepcion tipo,
        string identificacion,
        string responsable,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(envio);

        if (!Enum.IsDefined(tipo))
            throw new ArgumentException("El tipo de discrepancia no es válido.");

        if (string.IsNullOrWhiteSpace(identificacion))
            throw new ArgumentException("Debe indicarse la identificación del bulto.");

        if (string.IsNullOrWhiteSpace(responsable))
            throw new ArgumentException("Debe identificarse al responsable.");

        return new DiscrepanciaRecepcion
        {
            Id = Guid.NewGuid(),
            OperadorId = envio.OperadorId,
            EnvioId = envio.Id,
            Tipo = tipo,
            Identificacion = identificacion.Trim(),
            Responsable = responsable.Trim(),
            RegistradaEn = ahora
        };
    }
}
