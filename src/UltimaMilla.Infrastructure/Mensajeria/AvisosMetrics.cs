using System.Diagnostics.Metrics;

namespace UltimaMilla.Infrastructure.Mensajeria;

public static class AvisosMetrics
{
    public const string MeterName = "UltimaMilla.Avisos";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Entregados =
        Meter.CreateCounter<long>(
            "avisos_entregados_total",
            description: "Avisos a comercios entregados con 2xx");

    private static readonly Counter<long> Fallidos =
        Meter.CreateCounter<long>(
            "avisos_fallidos_total",
            description: "Intentos de aviso a comercios que fallaron");

    public static void RegistrarEntregado()
    {
        Entregados.Add(1);
    }

    public static void RegistrarFallido()
    {
        Fallidos.Add(1);
    }
}
