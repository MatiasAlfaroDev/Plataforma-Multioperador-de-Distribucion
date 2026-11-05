using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Repositorio de entregas de avisos (historial de envíos).
/// </summary>
public interface IEntregaAvisoRepository
{
    /// <summary>
    /// Obtiene una entrega por su identificador.
    /// </summary>
    Task<EntregaAviso?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Verifica si ya existe una entrega para esa suscripción y clave de evento.
    /// </summary>
    Task<bool> ExisteAsync(Guid suscripcionId, string claveEvento, CancellationToken ct);

    /// <summary>
    /// Lista las entregas de avisos filtradas por cuenta comercial y estado.
    /// </summary>
    Task<IReadOnlyList<EntregaAviso>> ListarAsync(Guid? cuentaComercialId, EstadoEntregaAviso? estado, CancellationToken ct);

    /// <summary>
    /// Agrega una nueva entrega a la base de datos.
    /// </summary>
    Task AgregarAsync(EntregaAviso entrega, CancellationToken ct);
}
