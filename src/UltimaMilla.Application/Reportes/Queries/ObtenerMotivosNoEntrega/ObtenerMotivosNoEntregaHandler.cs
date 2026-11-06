
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerMotivosNoEntrega;

public sealed class ObtenerMotivosNoEntregaHandler(
    IReporteEnviosRepository repositorio)
{
    public async Task<MotivosNoEntregaDto> Handle(
        Guid operadorId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        if (hasta < desde || hasta == DateOnly.MaxValue)
            throw new ArgumentException(
                "El rango de fechas no es válido.");

        var inicio = new DateTimeOffset(
            desde.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        var finExclusivo = new DateTimeOffset(
            hasta.AddDays(1).ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc));

        var conteos = await repositorio.ContarMotivosNoEntregaAsync(
            operadorId,
            inicio,
            finExclusivo,
            ct);

        var motivos = conteos
            .Select(x => new MotivoNoEntregaResumenDto(
                x.Codigo,
                x.Descripcion,
                x.IntentosFallidos,
                x.BultosNoEntregados,
                x.IntentosFallidos + x.BultosNoEntregados))
            .ToList();

        return new MotivosNoEntregaDto(
            desde,
            hasta,
            motivos.Sum(x => x.TotalIncidencias),
            motivos);
    }
}
