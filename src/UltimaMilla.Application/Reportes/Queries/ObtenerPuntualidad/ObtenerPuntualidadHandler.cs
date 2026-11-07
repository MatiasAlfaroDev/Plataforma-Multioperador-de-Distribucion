
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerPuntualidad;

public sealed record PuntualidadDto(
    DateOnly Desde,
    DateOnly Hasta,
    ResultadoPuntualidadReporte Resultados);

public sealed class ObtenerPuntualidadHandler(
    IReporteEnviosRepository repositorio)
{
    public async Task<PuntualidadDto> Handle(
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

        var resultados = await repositorio.ObtenerPuntualidadAsync(
            operadorId, inicio, fin, ct);

        return new PuntualidadDto(desde, hasta, resultados);
    }
}
