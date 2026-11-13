using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UltimaMilla.Application.Abstractions.Caching;

namespace UltimaMilla.Infrastructure.Caching;

/// <summary>
/// Caché distribuida en Redis (aclaración 3.1): la comparten todas las réplicas de la API y el Worker,
/// así una invalidación en una instancia la ven todas. Si Redis no responde, cada operación se comporta
/// como un fallo de caché y el caso de uso sigue contra la base de datos, más lento pero sin cortarse.
/// </summary>
public sealed class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private IDatabase Database => redis.GetDatabase();

    public async Task<T?> ObtenerAsync<T>(
        string clave,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var valor = await Database.StringGetAsync(clave);

            if (valor.IsNullOrEmpty)
            {
                CacheMetrics.RegistrarMiss(clave);
                return default;
            }

            CacheMetrics.RegistrarHit(clave);
            return JsonSerializer.Deserialize<T>(valor.ToString());
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
        {
            RegistrarFalla(ex, clave);
            CacheMetrics.RegistrarMiss(clave);
            return default;
        }
    }

    public async Task GuardarAsync<T>(
        string clave,
        T valor,
        TimeSpan duracion,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            await Database.StringSetAsync(clave, JsonSerializer.Serialize(valor), duracion);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
        {
            RegistrarFalla(ex, clave);
        }
    }

    public async Task EliminarAsync(
        string clave,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            await Database.KeyDeleteAsync(clave);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
        {
            // Si no se pudo invalidar, la entrada vence sola por su expiración.
            RegistrarFalla(ex, clave);
        }
    }

    public async Task EliminarPorPrefijoAsync(
        string prefijo,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            // ponytail: SCAN por prefijo en cada servidor; alcanza para decenas de claves por operador.
            // Con miles, un contador de versión en la clave evita recorrerlas.
            foreach (var servidor in redis.GetServers().Where(s => s.IsConnected && !s.IsReplica))
            {
                var claves = new List<RedisKey>();
                await foreach (var clave in servidor.KeysAsync(pattern: prefijo + "*"))
                    claves.Add(clave);

                if (claves.Count > 0)
                    await Database.KeyDeleteAsync(claves.ToArray());
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
        {
            RegistrarFalla(ex, prefijo);
        }
    }

    private void RegistrarFalla(Exception ex, string clave)
    {
        CacheMetrics.RegistrarError(clave);
        logger.LogWarning(ex, "Redis no respondió para {Clave}: se sigue sin caché.", clave);
    }
}
