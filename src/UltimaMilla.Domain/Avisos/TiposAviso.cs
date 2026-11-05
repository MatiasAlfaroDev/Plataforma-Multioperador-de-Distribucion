namespace UltimaMilla.Domain.Avisos;

/// <summary>
/// Tipos de aviso (eventos) a los que un comercio puede suscribirse.
/// Se relaciona con RF 28 y RNF 6.9.
/// </summary>
public static class TiposAviso
{
    /// <summary>
    /// Se crea un envío.
    /// </summary>
    public const string EnvioCreado = "envio.creado";

    /// <summary>
    /// Se entrega un envío.
    /// </summary>
    public const string EnvioEntregado = "envio.entregado";

    /// <summary>
    /// No se entrega un envío.
    /// </summary>
    public const string EnvioNoEntregado = "envio.no_entregado";

    /// <summary>
    /// Todos los tipos de aviso disponibles.
    /// </summary>
    public static readonly IReadOnlyList<string> Todos = [EnvioCreado, EnvioEntregado, EnvioNoEntregado];
}
