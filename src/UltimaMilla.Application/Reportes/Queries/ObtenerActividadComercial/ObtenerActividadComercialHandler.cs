
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ObtenerActividadComercial;

public sealed record ActividadComercialDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<VolumenDiarioReporte> VolumenDiario,
    IReadOnlyList<ResumenComercioReporte> Comercios);

public sealed class ObtenerActividadComercialHandler(
    IReporteEnviosRepository repositorio)
{
    public async Task<ActividadComercialDto> Handle(
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

        var inicio = HoraUruguay.InicioDelDia(desde);

        var fin = HoraUruguay.InicioDelDia(hasta.AddDays(1));

        var volumen = await repositorio.ObtenerVolumenDiarioAsync(
            operadorId, inicio, fin, ct);

        var comercios = await repositorio.ObtenerPorComercioAsync(
            operadorId, inicio, fin, ct);

        return new ActividadComercialDto(
            desde, hasta, volumen, comercios);
    }
}
