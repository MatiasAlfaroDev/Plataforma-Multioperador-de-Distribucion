using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Avisos.Queries.ListarSuscripciones;

/// <summary>
/// Obtiene una lista de suscripciones a avisos según el alcance del usuario.
/// </summary>
public sealed class ListarSuscripcionesHandler(
    ISuscripcionAvisoRepository suscripciones,
    ITenantContext sesion)
{
    public async Task<IReadOnlyList<SuscripcionAvisoDto>> Handle(ListarSuscripcionesQuery query, CancellationToken ct)
    {
        var (_, cuenta) = AlcanceAvisos.Resolver(sesion);
        return (await suscripciones.ListarAsync(cuenta, ct))
            .Select(SuscripcionAvisoDto.Desde)
            .ToList();
    }
}
