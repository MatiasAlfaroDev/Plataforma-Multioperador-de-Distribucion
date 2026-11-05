using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Recorrido de los repartidores durante la jornada (RF 23).</summary>
public interface IPosicionRepartidorRepository
{
    /// <summary>De esos Ids, cuáles ya están guardados (para no repetirlos al reintentar).</summary>
    Task<IReadOnlySet<Guid>> ExistentesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task AgregarAsync(IEnumerable<PosicionRepartidor> posiciones, CancellationToken ct);

    /// <summary>El recorrido de un repartidor entre dos momentos, en orden.</summary>
    Task<IReadOnlyList<PosicionRepartidor>> ListarRecorridoAsync(
        Guid operadorId, Guid repartidorId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct);
}
