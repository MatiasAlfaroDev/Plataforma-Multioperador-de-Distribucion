using System.Diagnostics.Metrics;
using StackExchange.Redis;

namespace UltimaMilla.Infrastructure.Observabilidad;

/// <summary>
/// Sincronizaciones pendientes (RNF 6.12): lo que todavía no subió cada app. Solo el teléfono lo sabe, así que al
/// terminar cada sincronización avisa cuántos eventos le quedan (PUT /api/repartidor/sincronizacion) y la API lo
/// guarda en un hash de Redis por usuario; el Worker publica la suma como métrica.
/// </summary>
public static class SincronizacionMetrics
{
    public const string MeterName = "UltimaMilla.Sincronizacion";

    // ponytail: el último valor de cada repartidor queda hasta que vuelva a avisar; si hiciera falta,
    // vencimiento por campo (HEXPIRE, Redis 7.4).
    public const string ClaveRedis = "sincronizacion:pendientes";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Lo llama el Worker al arrancar: una sola fuente, aunque la API tenga dos réplicas.</summary>
    public static void Observar(IConnectionMultiplexer redis) =>
        Meter.CreateObservableGauge(
            "sincronizaciones_pendientes",
            () => Leer(redis),
            description: "Eventos registrados sin conexión que las apps todavía no subieron");

    private static Measurement<long>[] Leer(IConnectionMultiplexer redis)
    {
        try
        {
            return [new(redis.GetDatabase().HashValues(ClaveRedis).Sum(v => (long)v))];
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            // Sin Redis no hay dato: mejor un hueco en el gráfico que un cero falso.
            return [];
        }
    }
}
