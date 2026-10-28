using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;

namespace UltimaMilla.Application.Configuracion.Queries.ObtenerZona;

public sealed class ObtenerZonaHandler(IZonaCoberturaRepository zonas)
{
    public async Task<ZonaDetalleDto> Handle(
        ObtenerZonaQuery query,
        CancellationToken ct)
    {
        var zona = await zonas.ObtenerAsync(query.OperadorId, query.ZonaId, ct)
            ?? throw new NoEncontradoException("La zona no existe.");

        return new ZonaDetalleDto(
            zona.Id,
            zona.Nombre,
            zona.Departamento,
            zona.MinutosTrasladoEstimado,
            zona.Localidades
                .OrderBy(l => l.Nombre)
                .ThenBy(l => l.Sector)
                .Select(l => new CoberturaDto(l.Nombre, l.Sector))
                .ToList(),
            zona.Franjas
                .OrderBy(f => f.HoraDesde)
                .Select(f => new FranjaDto(f.HoraDesde, f.HoraHasta))
                .ToList());
    }
}
