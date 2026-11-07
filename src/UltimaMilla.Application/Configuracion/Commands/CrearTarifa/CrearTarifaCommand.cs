namespace UltimaMilla.Application.Configuracion.Commands.CrearTarifa;

public sealed record CrearTarifaCommand(
    Guid OperadorId,
    Guid ZonaCoberturaId,
    string Nombre,
    string TipoServicio,
    decimal PrecioBase,
    decimal PrecioPorKg,
    decimal PrecioPorMetroCubico,
    decimal RecargoUrgentePorcentaje,
    decimal BonificacionPorcentaje);