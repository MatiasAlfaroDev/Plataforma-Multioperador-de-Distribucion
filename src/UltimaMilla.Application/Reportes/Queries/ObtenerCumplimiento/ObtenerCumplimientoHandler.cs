
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerCumplimiento;

public sealed record CumplimientoDto(
    DateOnly Desde,
    DateOnly Hasta,
    CumplimientoReporte Resultados);

public sealed class ObtenerCumplimientoHandler(
    IReporteEnviosRepository repositorio)
{
    public async Task<CumplimientoDto> Handle(
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

        var resultados = await repositorio.ObtenerCumplimientoAsync(
            operadorId,
            inicio,
            fin,
            ct);

        return new CumplimientoDto(desde, hasta, resultados);
    }
}
