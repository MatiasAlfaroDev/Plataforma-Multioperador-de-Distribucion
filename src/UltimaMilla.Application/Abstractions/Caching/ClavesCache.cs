namespace UltimaMilla.Application.Abstractions.Caching;

/// <summary>
/// Claves de la caché (aclaración 3.1). El primer segmento es el caso de uso (etiqueta de las métricas)
/// y las de configuración llevan el operador, así una entrada nunca se comparte entre inquilinos.
/// </summary>
public static class ClavesCache
{
    /// <summary>
    /// Seguimiento público: mucho volumen y cambia varias veces al día. Expiración corta y, sobre todo,
    /// invalidación cuando el Worker procesa un evento del envío. La clave es el hash del token, que
    /// identifica un único envío de un único operador.
    /// </summary>
    public static string Seguimiento(string tokenHash) => $"seguimiento:{tokenHash}";

    public static readonly TimeSpan DuracionSeguimiento = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Configuración versionada: una versión publicada no cambia, solo cambia cuál está vigente.
    /// Expiración larga; se invalida el prefijo del operador al publicar una versión nueva.
    /// </summary>
    public static string PrefijoTarifas(Guid operadorId) => $"tarifa-vigente:{operadorId}:";

    public static string TarifaVigente(Guid operadorId, Guid zonaId, string tipoServicio) =>
        $"{PrefijoTarifas(operadorId)}{zonaId}:{tipoServicio}";

    public static string ReglasVigentes(Guid operadorId) => $"reglas-vigentes:{operadorId}";

    public static readonly TimeSpan DuracionConfiguracion = TimeSpan.FromHours(1);
}
