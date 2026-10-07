using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Configuracion.Queries.ListarZonas;

public sealed class ListarZonasHandler(IZonaCoberturaRepository zonas)
{
    public async Task<IReadOnlyList<ZonaDto>> Handle(
        ListarZonasQuery query,
        CancellationToken ct)
    {
        var lista = await zonas.ListarPorOperadorAsync(
            query.OperadorId,
            ct);

        return lista
            .Select(z => new ZonaDto(
                z.Id,
                z.Nombre,
                z.Departamento))
            .ToList();
    }
}