using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerResumenRetiros;

public sealed record ResumenRetirosDto(
    DateOnly Desde,
    DateOnly Hasta,
    int TotalRetiros,
    decimal TotalTarifado,
    IReadOnlyList<ConteoRetirosPorEstado> PorEstado);

public sealed class ObtenerResumenRetirosHandler(
    IReporteRetirosRepository repositorio,
    ITenantContext tenant)
{
    public async Task<ResumenRetirosDto> Handle(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        if (tenant.OperadorId is not Guid operadorId ||
            operadorId == Guid.Empty)
            throw new UnauthorizedAccessException(
                "No hay un operador autenticado.");

        if (hasta < desde || hasta == DateOnly.MaxValue)
            throw new ArgumentException(
                "El rango de fechas no es válido.");

        // Misma convención UTC del reporte actual de envíos.
        var inicio = new DateTimeOffset(
            desde.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        var finExclusivo = new DateTimeOffset(
            hasta.AddDays(1).ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        var estados = await repositorio.ContarPorEstadoAsync(
            operadorId, inicio, finExclusivo, ct);

        var totalTarifado =
            await repositorio.ObtenerTotalTarifadoAsync(
                operadorId, inicio, finExclusivo, ct);

        return new ResumenRetirosDto(
            desde,
            hasta,
            estados.Sum(x => x.Cantidad),
            totalTarifado,
            estados);
    }
}