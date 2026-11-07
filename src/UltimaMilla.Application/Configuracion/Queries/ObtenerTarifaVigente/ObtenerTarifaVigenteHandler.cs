using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;

public sealed class ObtenerTarifaVigenteHandler(
    IVersionTarifariaRepository tarifas,
    TimeProvider reloj)
{
    public async Task<TarifaVigenteDto?> Handle(
        ObtenerTarifaVigenteQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Enum.TryParse<TipoServicioTarifario>(
                query.TipoServicio,
                ignoreCase: true,
                out var tipoServicio) ||
            !Enum.IsDefined(tipoServicio))
        {
            throw new ArgumentException(
                "El tipo de servicio debe ser Entrega o RetiroDomicilio.");
        }

        var version = await tarifas.ObtenerVigentePorZonaAsync(
            query.OperadorId,
            query.ZonaCoberturaId,
            reloj.GetUtcNow(),
            ct,
            tipoServicio);

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
            version.VigenciaDesde,
            version.TipoServicio.ToString());
    }
}