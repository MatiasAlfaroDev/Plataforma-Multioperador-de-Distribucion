
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerResumenEnvios;

public sealed class ObtenerResumenEnviosHandler(
    IReporteEnviosRepository repositorio)
{
    public async Task<ResumenEnviosDto> Handle(
        Guid operadorId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.");

        if (hasta < desde)
            throw new ArgumentException(
                "La fecha hasta no puede ser anterior a la fecha desde.");

        if (hasta == DateOnly.MaxValue)
            throw new ArgumentException(
                "La fecha hasta está fuera del rango permitido.");

        // El período se interpreta en días de Uruguay, igual que el cierre diario.
        var inicio = HoraUruguay.InicioDelDia(desde);

        var finExclusivo = HoraUruguay.InicioDelDia(hasta.AddDays(1));

        var conteos = await repositorio.ContarPorEstadoAsync(
            operadorId,
            inicio,
            finExclusivo,
            ct);

        var porEstado = conteos
            .OrderBy(c => c.Estado)
            .Select(c => new EstadoEnvioResumenDto(
                c.Estado.ToString(),
                c.Cantidad))
            .ToList();

        return new ResumenEnviosDto(
            desde,
            hasta,
            porEstado.Sum(c => c.Cantidad),
            porEstado);
    }
}
