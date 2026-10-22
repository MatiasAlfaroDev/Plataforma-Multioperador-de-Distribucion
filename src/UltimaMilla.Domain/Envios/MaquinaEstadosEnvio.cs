using Stateless;

namespace UltimaMilla.Domain.Envios;

public static class MaquinaEstadosEnvio
{
    public static StateMachine<EstadoEnvio, DisparadorEnvio> Crear(
        Func<EstadoEnvio> obtenerEstado,
        Action<EstadoEnvio> cambiarEstado)
    {
        var maquina = new StateMachine<EstadoEnvio, DisparadorEnvio>(
            obtenerEstado, cambiarEstado);

        maquina.Configure(EstadoEnvio.Admitido)
            .Permit(DisparadorEnvio.RecibirEnDeposito, EstadoEnvio.EnDeposito);

        maquina.Configure(EstadoEnvio.EnDeposito)
            .Permit(DisparadorEnvio.AsignarARuta, EstadoEnvio.AsignadoARuta)
            .Permit(DisparadorEnvio.Reprogramar, EstadoEnvio.Reprogramado);

        maquina.Configure(EstadoEnvio.AsignadoARuta)
            .Permit(DisparadorEnvio.QuitarDeRuta, EstadoEnvio.EnDeposito)
            .Permit(DisparadorEnvio.Despachar, EstadoEnvio.EnTransito);

        maquina.Configure(EstadoEnvio.EnTransito)
            .Permit(DisparadorEnvio.Entregar, EstadoEnvio.Entregado)
            .Permit(DisparadorEnvio.RegistrarIntentoFallido, EstadoEnvio.NoEntregado);

        maquina.Configure(EstadoEnvio.NoEntregado)
            .Permit(DisparadorEnvio.RetornarADeposito, EstadoEnvio.EnDeposito)
            .Permit(DisparadorEnvio.IniciarDevolucion, EstadoEnvio.EnDevolucion);

        maquina.Configure(EstadoEnvio.Reprogramado)
            .Permit(DisparadorEnvio.AsignarARuta, EstadoEnvio.AsignadoARuta);

        maquina.Configure(EstadoEnvio.EnDevolucion)
            .Permit(DisparadorEnvio.ConfirmarDevolucion, EstadoEnvio.Devuelto);

        maquina.Configure(EstadoEnvio.Extraviado)
            .Permit(DisparadorEnvio.Reaparecer, EstadoEnvio.EnDeposito);

        // Extraviado es accesible desde todos los estados no finales.
        EstadoEnvio[] estadosNoFinales =
        [
            EstadoEnvio.EnDeposito,
            EstadoEnvio.AsignadoARuta,
            EstadoEnvio.EnTransito,
            EstadoEnvio.NoEntregado,
            EstadoEnvio.Reprogramado,
            EstadoEnvio.EnDevolucion
        ];

        foreach (var estado in estadosNoFinales)
        {
            maquina.Configure(estado)
                .Permit(DisparadorEnvio.DeclararExtraviado, EstadoEnvio.Extraviado);
        }

        return maquina;
    }
}