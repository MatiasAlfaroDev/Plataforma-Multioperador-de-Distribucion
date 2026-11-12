using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Repositorio de suscripciones a avisos (webhooks) para notificaciones push.
/// </summary>
public interface ISuscripcionAvisoRepository
{
    /// <summary>
    /// Obtiene una suscripción por su identificador.
    /// </summary>
    Task<SuscripcionAviso?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Lista las suscripciones de un comercio o todas del operador si es null.
    /// </summary>
    Task<IReadOnlyList<SuscripcionAviso>> ListarAsync(IReadOnlyCollection<Guid>? cuentasComerciales, CancellationToken ct);

    /// <summary>
    /// Agrega una nueva suscripción a la base de datos.
    /// </summary>
    Task AgregarAsync(SuscripcionAviso suscripcion, CancellationToken ct);
}
