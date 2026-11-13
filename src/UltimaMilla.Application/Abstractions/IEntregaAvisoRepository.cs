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
    /// Obtiene una entrega dentro de las cuentas indicadas (sesión de comercio, sin operador) o,
    /// si es null, del operador actual.
    /// </summary>
    Task<EntregaAviso?> ObtenerAsync(Guid id, IReadOnlyCollection<Guid>? cuentasComerciales, CancellationToken ct);

    /// <summary>
    /// Verifica si ya existe una entrega para esa suscripción y clave de evento.
    /// </summary>
    Task<bool> ExisteAsync(Guid suscripcionId, string claveEvento, CancellationToken ct);

    /// <summary>
    /// Lista las entregas de avisos filtradas por cuenta comercial y estado.
    /// </summary>
    Task<IReadOnlyList<EntregaAviso>> ListarAsync(IReadOnlyCollection<Guid>? cuentasComerciales, EstadoEntregaAviso? estado, CancellationToken ct);

    /// <summary>
    /// Entregas fallidas del operador actual que el reintento programado puede volver a encolar.
    /// </summary>
    Task<IReadOnlyList<EntregaAviso>> ListarFallidasAsync(CancellationToken ct);

    /// <summary>
    /// Operadores con entregas fallidas sin agotar. Ignora el filtro por operador: la usa la tarea programada, que no tiene inquilino.
    /// </summary>
    Task<IReadOnlyList<Guid>> OperadoresConFallidasAsync(CancellationToken ct);

    /// <summary>
    /// Agrega una nueva entrega a la base de datos.
    /// </summary>
    Task AgregarAsync(EntregaAviso entrega, CancellationToken ct);
}
