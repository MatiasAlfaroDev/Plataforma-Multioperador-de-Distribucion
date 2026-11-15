
namespace UltimaMilla.Domain.Retiros;

public enum EstadoRetiro
{
    Solicitado,
    AsignadoARuta,
    Retirado,
    NoRetirado,
    EnDeposito,
    EntregadoAlComercio,
    EnTransito,

    // Segunda distribución: depósito -> comercio
    AsignadoARutaComercio,
    EnTransitoComercio
}

public enum DisparadorRetiro
{
    AsignarARuta,
    RegistrarRetiro,
    RegistrarRetiroFallido,
    Reprogramar,
    RecibirEnDeposito,
    EntregarAlComercio,
    Despachar,

    // Segunda distribución
    AsignarARutaComercio,
    DespacharAlComercio
}
