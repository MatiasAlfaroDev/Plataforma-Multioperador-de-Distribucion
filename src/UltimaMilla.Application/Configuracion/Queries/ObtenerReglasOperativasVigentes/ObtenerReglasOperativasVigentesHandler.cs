using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Configuracion.Queries.ObtenerReglasOperativasVigentes;

public sealed class ObtenerReglasOperativasVigentesHandler(
    IVersionReglasOperativasRepository reglas,
    IMotivoNoEntregaRepository motivosRepository,
    TimeProvider reloj)
{
    public async Task<ReglasOperativasVigentesDto?> Handle(
        ObtenerReglasOperativasVigentesQuery query,
        CancellationToken ct)
    {
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

        return new ReglasOperativasVigentesDto(
            version.Id,
            version.MaximoIntentosEntrega,
            version.HorasMinimasEntreIntentos,
            version.RequierePruebaEntrega,
            version.DiasMaximosParaDevolucion,
            version.VigenciaDesde,
            motivos.Select(m => new MotivoNoEntregaDto(
                m.Codigo,
                m.Descripcion,
                m.RequiereDevolucion)).ToList());
    }
}