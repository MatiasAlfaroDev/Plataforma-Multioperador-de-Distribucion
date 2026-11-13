using System.Diagnostics.Metrics;

namespace UltimaMilla.Infrastructure.Caching;

/// <summary>
/// Aciertos, fallos y errores de la caché por caso de uso (aclaración 3.1): las abstracciones de .NET
/// no los miden. El caso de uso es el primer segmento de la clave ("seguimiento", "tarifa-vigente", ...).
/// </summary>
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

    private static readonly Counter<long> Errores =
        Meter.CreateCounter<long>(
            "cache_errores_total",
            description: "Operaciones en las que Redis no respondió y se siguió contra la base de datos");

    public static void RegistrarHit(string clave) => Hits.Add(1, Etiqueta(clave));

    public static void RegistrarMiss(string clave) => Misses.Add(1, Etiqueta(clave));

    public static void RegistrarError(string clave) => Errores.Add(1, Etiqueta(clave));

    private static KeyValuePair<string, object?> Etiqueta(string clave) =>
        new("caso_de_uso", clave.Split(':', 2)[0]);
}
