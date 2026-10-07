namespace UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;

public sealed record ActualizarTarifaCommand(
    Guid OperadorId,
    Guid CuadroTarifarioId,
    Guid ZonaCoberturaId,
    decimal PrecioBase,
    decimal PrecioPorKg,
    decimal PrecioPorMetroCubico,
    decimal RecargoUrgentePorcentaje,
    decimal BonificacionPorcentaje);