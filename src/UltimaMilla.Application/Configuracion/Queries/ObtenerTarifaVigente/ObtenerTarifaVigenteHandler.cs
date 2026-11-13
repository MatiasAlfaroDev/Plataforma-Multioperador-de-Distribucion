using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;

public sealed class ObtenerTarifaVigenteHandler(
    IVersionTarifariaRepository tarifas,
    TimeProvider reloj,
    ICacheService cache)
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

        var clave = ClavesCache.TarifaVigente(query.OperadorId, query.ZonaCoberturaId, tipoServicio.ToString());
        if (await cache.ObtenerAsync<TarifaVigenteDto>(clave, ct) is { } enCache)
            return enCache;

        var version = await tarifas.ObtenerVigentePorZonaAsync(
            query.OperadorId,
            query.ZonaCoberturaId,
            reloj.GetUtcNow(),
            ct,
            tipoServicio);

        if (version is null)
            return null;

        var dto = new TarifaVigenteDto(
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

        await cache.GuardarAsync(clave, dto, ClavesCache.DuracionConfiguracion, ct);
        return dto;
    }
}