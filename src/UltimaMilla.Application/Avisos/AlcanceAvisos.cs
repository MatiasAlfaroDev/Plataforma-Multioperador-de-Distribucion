using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Avisos;

/// <summary>
/// Qué avisos y suscripciones puede ver la sesión actual.
/// El operador ve todo lo suyo (Cuentas = null); el comercio, solo lo de sus cuentas confirmadas,
/// que pueden ser de varios operadores (letra 3.3).
/// </summary>
public sealed record AlcanceAvisos(Guid? OperadorId, IReadOnlyDictionary<Guid, Guid>? Cuentas)
{
    /// <summary>Operador dueño de la cuenta, o null si la sesión no puede usarla.</summary>
    public Guid? OperadorDeCuenta(Guid cuentaComercialId)
    {
        if (Cuentas is null)
            return OperadorId;

        return Cuentas.TryGetValue(cuentaComercialId, out var operadorId) ? operadorId : null;
    }

    /// <exception cref="UnauthorizedAccessException">Si no hay sesión o no tiene permisos.</exception>
    public static async Task<AlcanceAvisos> ResolverAsync(
        ITenantContext sesion, ICuentaComercialRepository cuentas, CancellationToken ct)
    {
        if (!sesion.EstaAutenticado)
            throw new UnauthorizedAccessException("Debe iniciar sesión.");

        if (sesion.Perfil is Perfiles.Comercio or Perfiles.AdministradorComercio)
        {
            var comercioId = sesion.ComercioId
                ?? throw new UnauthorizedAccessException("No tiene un comercio seleccionado.");

            var confirmadas = (await cuentas.ListarPorComercioAsync(comercioId, ct))
                .Where(c => c.Activa && c.Estado == EstadoCuentaComercial.Confirmada)
                .ToDictionary(c => c.Id, c => c.OperadorId);

            return new AlcanceAvisos(null, confirmadas);
        }

        if (Perfiles.Despacho.Contains(sesion.Perfil) && sesion.OperadorId is { } operadorId)
            return new AlcanceAvisos(operadorId, null);

        throw new UnauthorizedAccessException("No tiene permisos sobre los avisos.");
    }
}
