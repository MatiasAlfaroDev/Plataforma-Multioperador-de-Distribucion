using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;

public sealed class ObtenerTarifaVigenteHandler(
    IVersionTarifariaRepository tarifas,
    TimeProvider reloj)
{
    public async Task<TarifaVigenteDto?> Handle(
        ObtenerTarifaVigenteQuery query,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();

        var version = await tarifas.ObtenerVigentePorZonaAsync(
            query.OperadorId,
            query.ZonaCoberturaId,
            ahora,
            ct);

        if (version is null)
            return null;

        return new TarifaVigenteDto(
            version.Id,
            version.CuadroTarifarioId,
            version.ZonaCoberturaId,
            version.PrecioBase,
            version.PrecioPorKg,
            version.PrecioPorMetroCubico,
            version.RecargoUrgentePorcentaje,
            version.BonificacionPorcentaje,
            version.VigenciaDesde);
    }
}