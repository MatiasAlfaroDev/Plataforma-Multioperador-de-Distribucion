using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Avisos.Queries.ListarAvisos;

/// <summary>
/// Obtiene una lista de entregas de avisos según el estado y el alcance del usuario.
/// </summary>
public sealed class ListarAvisosHandler(
    IEntregaAvisoRepository entregas,
    ITenantContext sesion)
{
    public async Task<IReadOnlyList<EntregaAvisoDto>> Handle(ListarAvisosQuery query, CancellationToken ct)
    {
        var (_, cuenta) = AlcanceAvisos.Resolver(sesion);
        return (await entregas.ListarAsync(cuenta, query.Estado, ct))
            .Select(EntregaAvisoDto.Desde)
            .ToList();
    }
}
