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

    /// <summary>
    /// RF 31: el resultado de una visita —y con él el estado del envío— se deriva de los bultos.
    /// Todos los pendientes entregados: Entregado; algunos: EntregadoParcialmente; ninguno: NoEntregado.
    /// </summary>
    public static DisparadorEnvio ResultadoDeVisita(int pendientes, int entregados)
    {
        if (entregados < 0 || entregados > pendientes)
            throw new ArgumentOutOfRangeException(nameof(entregados), "No se pueden entregar más bultos que los pendientes.");

        if (entregados == 0)
            return DisparadorEnvio.RegistrarIntentoFallido;

        return entregados == pendientes
            ? DisparadorEnvio.Entregar
            : DisparadorEnvio.RegistrarEntregaParcial;
    }

    /// <summary>Resultado que queda en el historial del bulto según el disparador que se le aplicó.</summary>
    public static ResultadoBulto Resultado(Disparador disparador) => disparador switch
    {
        Disparador.Entregar => ResultadoBulto.Entregado,
        Disparador.IniciarDevolucion => ResultadoBulto.EnDevolucion,
        Disparador.ConfirmarDevolucion => ResultadoBulto.Devuelto,
        Disparador.DeclararExtraviado => ResultadoBulto.Extraviado,
        Disparador.Reaparecer => ResultadoBulto.Reaparecido,
        _ => throw new ArgumentOutOfRangeException(nameof(disparador))
    };
}
