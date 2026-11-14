namespace UltimaMilla.Domain.Retiros;

public enum EstadoRetiro
{
    Solicitado,
    AsignadoARuta,
    Retirado,
    NoRetirado,
    EnDeposito,
    EntregadoAlComercio,
    Cancelado
}

public enum DisparadorRetiro
{
    AsignarARuta,
    RegistrarRetiro,
    RegistrarRetiroFallido,
    Reprogramar,
    RecibirEnDeposito,
    EntregarAlComercio,
    Cancelar,
    Desasignar
}