
using System.Diagnostics.Metrics;

namespace UltimaMilla.Infrastructure.Caching;

public static class CacheMetrics
{
    public const string MeterName = "UltimaMilla.Cache";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Hits =
        Meter.CreateCounter<long>(
            "cache_hits_total",
            description: "Consultas resueltas desde Redis");

    private static readonly Counter<long> Misses =
        Meter.CreateCounter<long>(
            "cache_misses_total",
            description: "Consultas sin resultado en Redis");

    public static void RegistrarHit()
    {
        Hits.Add(1);
    }

    public static void RegistrarMiss()
    {
        Misses.Add(1);
    }
}
