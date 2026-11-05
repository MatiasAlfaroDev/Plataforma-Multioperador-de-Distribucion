namespace UltimaMilla.Domain.Envios;

public enum EstadoEnvio
{
    Admitido,
    EnDeposito,
    AsignadoARuta,
    EnTransito,
    Entregado,
    EntregadoParcialmente,
    NoEntregado,
    Reprogramado,
    EnDevolucion,
    Devuelto,
    Extraviado
}
