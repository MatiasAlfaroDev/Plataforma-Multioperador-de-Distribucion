
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Domain.Avisos;

/// <summary>
/// Tipos de aviso a los que un comercio puede suscribirse.
/// Uno por el alta y uno por cada cambio de estado del envío.
/// Se relaciona con RF 28 y RNF 6.9.
/// </summary>
public static class TiposAviso
{
    public const string EnvioCreado = "envio.creado";
    public const string EnvioEnDeposito = "envio.en_deposito";
    public const string EnvioAsignadoARuta = "envio.asignado_a_ruta";
    public const string EnvioEnTransito = "envio.en_transito";
    public const string EnvioEntregado = "envio.entregado";
    public const string EnvioEntregadoParcialmente = "envio.entregado_parcialmente";
    public const string EnvioNoEntregado = "envio.no_entregado";
    public const string EnvioReprogramado = "envio.reprogramado";
    public const string EnvioEnDevolucion = "envio.en_devolucion";
    public const string EnvioDevuelto = "envio.devuelto";
    public const string EnvioExtraviado = "envio.extraviado";

    /// <summary>
    /// Todos los tipos de aviso disponibles.
    /// </summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        EnvioCreado,
        EnvioEnDeposito,
        EnvioAsignadoARuta,
        EnvioEnTransito,
        EnvioEntregado,
        EnvioEntregadoParcialmente,
        EnvioNoEntregado,
        EnvioReprogramado,
        EnvioEnDevolucion,
        EnvioDevuelto,
        EnvioExtraviado
    ];

    /// <summary>
    /// Tipo de aviso correspondiente a cada estado.
    /// Admitido no tiene porque el alta se avisa con EnvioCreado.
    /// </summary>
    public static string? DeEstado(EstadoEnvio estado) => estado switch
    {
        EstadoEnvio.EnDeposito => EnvioEnDeposito,
        EstadoEnvio.AsignadoARuta => EnvioAsignadoARuta,
        EstadoEnvio.EnTransito => EnvioEnTransito,
        EstadoEnvio.Entregado => EnvioEntregado,
        EstadoEnvio.EntregadoParcialmente => EnvioEntregadoParcialmente,
        EstadoEnvio.NoEntregado => EnvioNoEntregado,
        EstadoEnvio.Reprogramado => EnvioReprogramado,
        EstadoEnvio.EnDevolucion => EnvioEnDevolucion,
        EstadoEnvio.Devuelto => EnvioDevuelto,
        EstadoEnvio.Extraviado => EnvioExtraviado,
        _ => null
    };
}
