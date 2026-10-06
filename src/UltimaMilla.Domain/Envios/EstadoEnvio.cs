namespace UltimaMilla.Domain.Envios;

/// <summary>
/// Los diez estados de la máquina de estados (informe, sección 4).
/// En el esqueleto solo se usa Admitido; las transiciones con Stateless son del monitoreo del 15/10.
/// </summary>
public enum EstadoEnvio
{
    Admitido,
    EnDeposito,
    AsignadoARuta,
    EnTransito,
    Entregado,
    NoEntregado,
    Reprogramado,
    EnDevolucion,
    Devuelto,
    Extraviado
}
