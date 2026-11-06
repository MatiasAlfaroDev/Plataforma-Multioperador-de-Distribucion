
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerResultadosRepartidores;

public sealed record ResultadosRepartidoresDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<ResultadoRepartidorReporte> Repartidores);

public sealed class ObtenerResultadosRepartidoresHandler(
    IReporteEnviosRepository repositorio)
{
    public async Task<ResultadosRepartidoresDto> Handle(
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
            desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var fin = new DateTimeOffset(
            hasta.AddDays(1).ToDateTime(
                TimeOnly.MinValue, DateTimeKind.Utc));

        var resultados =
            await repositorio.ObtenerResultadosPorRepartidorAsync(
                operadorId, inicio, fin, ct);

        return new ResultadosRepartidoresDto(
            desde, hasta, resultados);
    }
}
