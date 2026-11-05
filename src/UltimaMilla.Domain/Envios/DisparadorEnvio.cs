namespace UltimaMilla.Domain.Envios;

public enum DisparadorEnvio
{
    RecibirEnDeposito,
    AsignarARuta,
    QuitarDeRuta,
    Despachar,
    Entregar,
    RegistrarEntregaParcial,
    RegistrarIntentoFallido,
    RetornarADeposito,
    IniciarDevolucion,
    Reprogramar,
    ConfirmarDevolucion,
    DeclararExtraviado,
    Reaparecer
}
