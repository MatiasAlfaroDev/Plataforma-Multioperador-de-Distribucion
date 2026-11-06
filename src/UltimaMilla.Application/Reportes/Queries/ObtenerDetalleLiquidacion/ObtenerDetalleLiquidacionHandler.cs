
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerDetalleLiquidacion;

public sealed record DetalleLiquidacionDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<DetalleLiquidacionComercio> Comercios);

public sealed class ObtenerDetalleLiquidacionHandler(
    IReporteEnviosRepository repositorio)
{
    public async Task<DetalleLiquidacionDto> Handle(
        Guid operadorId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (hasta < desde || hasta == DateOnly.MaxValue)
            throw new ArgumentException("Rango de fechas no válido.");

        var inicio = new DateTimeOffset(
            desde.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        var fin = new DateTimeOffset(
            hasta.AddDays(1).ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        var comercios =
            await repositorio.ObtenerDetalleLiquidacionAsync(
                operadorId,
                inicio,
                fin,
                ct);

        return new DetalleLiquidacionDto(
            desde, hasta, comercios);
    }
}
