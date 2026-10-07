using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Domain.Configuracion;

public class VersionTarifaria : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public Guid CuadroTarifarioId { get; private set; }
    public Guid ZonaCoberturaId { get; private set; }

    public decimal PrecioBase { get; private set; }
    public decimal PrecioPorKg { get; private set; }
    public decimal PrecioPorMetroCubico { get; private set; }

    public decimal RecargoUrgentePorcentaje { get; private set; }
    public decimal BonificacionPorcentaje { get; private set; }

    public DateTimeOffset VigenciaDesde { get; private set; }
    public DateTimeOffset? VigenciaHasta { get; private set; }

    private VersionTarifaria() { }

    public static VersionTarifaria Crear(
        Guid operadorId,
        Guid cuadroTarifarioId,
        Guid zonaCoberturaId,
        decimal precioBase,
        decimal precioPorKg,
        decimal precioPorMetroCubico,
        decimal recargoUrgentePorcentaje,
        decimal bonificacionPorcentaje,
        DateTimeOffset vigenciaDesde)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));
        if (cuadroTarifarioId == Guid.Empty)
            throw new ArgumentException("El cuadro tarifario es obligatorio.", nameof(cuadroTarifarioId));
        if (zonaCoberturaId == Guid.Empty)
            throw new ArgumentException("La zona de cobertura es obligatoria.", nameof(zonaCoberturaId));

        if (precioBase < 0 || precioPorKg < 0 || precioPorMetroCubico < 0)
            throw new ArgumentException("Los precios no pueden ser negativos.");

        if (recargoUrgentePorcentaje < 0)
            throw new ArgumentException("El recargo no puede ser negativo.");

        if (bonificacionPorcentaje < 0 || bonificacionPorcentaje > 100)
            throw new ArgumentException("La bonificación debe estar entre 0 y 100.");

        return new VersionTarifaria
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            CuadroTarifarioId = cuadroTarifarioId,
            ZonaCoberturaId = zonaCoberturaId,
            PrecioBase = precioBase,
            PrecioPorKg = precioPorKg,
            PrecioPorMetroCubico = precioPorMetroCubico,
            RecargoUrgentePorcentaje = recargoUrgentePorcentaje,
            BonificacionPorcentaje = bonificacionPorcentaje,
            VigenciaDesde = vigenciaDesde
        };
    }

    public bool EstaVigente(DateTimeOffset fecha)
    {
        return fecha >= VigenciaDesde &&
               (VigenciaHasta is null || fecha < VigenciaHasta);
    }

    public decimal Calcular(
        decimal pesoKg,
        decimal volumenMetrosCubicos,
        Modalidad modalidad)
    {
        var tarifa =
            PrecioBase +
            (pesoKg * PrecioPorKg) +
            (volumenMetrosCubicos * PrecioPorMetroCubico);

        if (modalidad == Modalidad.Urgente)
            tarifa += tarifa * RecargoUrgentePorcentaje / 100m;

        if (BonificacionPorcentaje > 0)
            tarifa -= tarifa * BonificacionPorcentaje / 100m;

        return Math.Round(tarifa, 2);
    }

    public void FinalizarVigencia(DateTimeOffset fecha)
    {
        if (fecha <= VigenciaDesde)
            throw new ArgumentException(
                "La fecha de fin debe ser posterior al inicio de vigencia.",
                nameof(fecha));

        VigenciaHasta = fecha;
    }
}