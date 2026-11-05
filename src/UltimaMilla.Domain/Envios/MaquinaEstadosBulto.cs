
using Stateless;

namespace UltimaMilla.Domain.Envios;

public static class MaquinaEstadosBulto
{
    public enum Disparador
    {
        Entregar,
        IniciarDevolucion,
        ConfirmarDevolucion,
        DeclararExtraviado,
        Reaparecer
    }

    public static StateMachine<EstadoBulto, Disparador> Crear(
        Func<EstadoBulto> obtenerEstado,
        Action<EstadoBulto> cambiarEstado)
    {
        var maquina = new StateMachine<EstadoBulto, Disparador>(
            obtenerEstado,
            cambiarEstado);

        maquina.Configure(EstadoBulto.Pendiente)
            .Permit(Disparador.Entregar, EstadoBulto.Entregado)
            .Permit(Disparador.IniciarDevolucion, EstadoBulto.EnDevolucion)
            .Permit(Disparador.DeclararExtraviado, EstadoBulto.Extraviado);

        maquina.Configure(EstadoBulto.EnDevolucion)
            .Permit(Disparador.ConfirmarDevolucion, EstadoBulto.Devuelto)
            .Permit(Disparador.DeclararExtraviado, EstadoBulto.Extraviado);

        maquina.Configure(EstadoBulto.Extraviado)
            .Permit(Disparador.Reaparecer, EstadoBulto.Pendiente);

        return maquina;
    }
}
