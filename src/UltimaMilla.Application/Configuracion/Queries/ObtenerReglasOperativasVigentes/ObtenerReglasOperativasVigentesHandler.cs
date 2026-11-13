using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;

namespace UltimaMilla.Application.Configuracion.Queries.ObtenerReglasOperativasVigentes;

public sealed class ObtenerReglasOperativasVigentesHandler(
    IVersionReglasOperativasRepository reglas,
    IMotivoNoEntregaRepository motivosRepository,
    TimeProvider reloj,
    ICacheService cache)
{
    public async Task<ReglasOperativasVigentesDto?> Handle(
        ObtenerReglasOperativasVigentesQuery query,
        CancellationToken ct)
    {
        var clave = ClavesCache.ReglasVigentes(query.OperadorId);
        if (await cache.ObtenerAsync<ReglasOperativasVigentesDto>(clave, ct) is { } enCache)
            return enCache;

        var ahora = reloj.GetUtcNow();

        var version = await reglas.ObtenerVigenteAsync(
            query.OperadorId,
            ahora,
            ct);

        if (version is null)
            return null;

        var motivos = await motivosRepository.ListarPorVersionAsync(
            query.OperadorId,
            version.Id,
            ct);

        var dto = new ReglasOperativasVigentesDto(
            version.Id,
            version.MaximoIntentosEntrega,
            version.HorasMinimasEntreIntentos,
            version.RequierePruebaEntrega,
            version.DiasMaximosParaDevolucion,
            version.VigenciaDesde,
            motivos.Select(m => new MotivoNoEntregaDto(
                m.Codigo,
                m.Descripcion,
                m.RequiereDevolucion)).ToList(),
            version.AdmiteEntregaParcial,
            version.AccionBultosPendientes,
            version.NuevaVisitaConRecargo);

        await cache.GuardarAsync(clave, dto, ClavesCache.DuracionConfiguracion, ct);
        return dto;
    }
}
