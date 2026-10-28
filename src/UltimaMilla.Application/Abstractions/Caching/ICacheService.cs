namespace UltimaMilla.Application.Abstractions.Caching;

public interface ICacheService
{
    Task<T?> ObtenerAsync<T>(
        string clave,
        CancellationToken ct = default);

    Task GuardarAsync<T>(
        string clave,
        T valor,
        TimeSpan duracion,
        CancellationToken ct = default);

    Task EliminarAsync(
        string clave,
        CancellationToken ct = default);
}
