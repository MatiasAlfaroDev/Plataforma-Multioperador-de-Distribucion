
using System.Text.Json;
using StackExchange.Redis;
using UltimaMilla.Application.Abstractions.Caching;

namespace UltimaMilla.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

   
    public async Task<T?> ObtenerAsync<T>(
        string clave,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var valor = await _database.StringGetAsync(clave);

        if (valor.IsNullOrEmpty)
        {
            CacheMetrics.RegistrarMiss();
            return default;
        }

        var resultado = JsonSerializer.Deserialize<T>(
            valor.ToString());

        CacheMetrics.RegistrarHit();

        return resultado;
    }


    public async Task GuardarAsync<T>(
        string clave,
        T valor,
        TimeSpan duracion,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(valor);

        await _database.StringSetAsync(
            clave,
            json,
            duracion);
    }

    public async Task EliminarAsync(
        string clave,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await _database.KeyDeleteAsync(clave);
    }
}
