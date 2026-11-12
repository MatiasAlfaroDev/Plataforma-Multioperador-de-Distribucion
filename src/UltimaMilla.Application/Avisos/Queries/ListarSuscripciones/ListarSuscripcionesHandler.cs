using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Avisos.Queries.ListarSuscripciones;

/// <summary>
/// Obtiene una lista de suscripciones a avisos según el alcance del usuario.
/// </summary>
public sealed class ListarSuscripcionesHandler(
    ISuscripcionAvisoRepository suscripciones,
    ICuentaComercialRepository cuentas,
    ITenantContext sesion)
{
    public async Task<IReadOnlyList<SuscripcionAvisoDto>> Handle(ListarSuscripcionesQuery query, CancellationToken ct)
    {
        var alcance = await AlcanceAvisos.ResolverAsync(sesion, cuentas, ct);
        return (await suscripciones.ListarAsync(alcance.Cuentas?.Keys.ToList(), ct))
            .Select(SuscripcionAvisoDto.Desde)
            .ToList();
    }
}
