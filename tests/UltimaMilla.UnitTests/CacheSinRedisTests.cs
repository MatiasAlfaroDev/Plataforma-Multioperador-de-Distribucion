using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Infrastructure.Caching;

namespace UltimaMilla.UnitTests;

/// <summary>Aclaración 3.1: si la caché no está disponible, el sistema sigue funcionando contra la base.</summary>
public class CacheSinRedisTests
{
    [Fact]
    public async Task Sin_Redis_la_cache_se_comporta_como_un_fallo_y_no_corta_el_caso_de_uso()
    {
        // Un puerto donde no escucha nadie: cada operación falla en Redis.
        var opciones = ConfigurationOptions.Parse("127.0.0.1:1");
        opciones.AbortOnConnectFail = false;
        opciones.ConnectTimeout = 200;
        opciones.SyncTimeout = 200;
        opciones.AsyncTimeout = 200;
        using var redis = await ConnectionMultiplexer.ConnectAsync(opciones);

        var cache = new RedisCacheService(redis, NullLogger<RedisCacheService>.Instance);
        var clave = ClavesCache.ReglasVigentes(Guid.NewGuid());

        Assert.Null(await cache.ObtenerAsync<string>(clave));
        await cache.GuardarAsync(clave, "valor", TimeSpan.FromMinutes(1));
        await cache.EliminarAsync(clave);
        await cache.EliminarPorPrefijoAsync(ClavesCache.PrefijoTarifas(Guid.NewGuid()));
    }

    [Fact]
    public void Las_claves_de_configuracion_distinguen_al_operador()
    {
        var zona = Guid.NewGuid();
        var a = ClavesCache.TarifaVigente(Guid.NewGuid(), zona, "Entrega");
        var b = ClavesCache.TarifaVigente(Guid.NewGuid(), zona, "Entrega");

        Assert.NotEqual(a, b);
        Assert.StartsWith("tarifa-vigente:", a);
    }
}
