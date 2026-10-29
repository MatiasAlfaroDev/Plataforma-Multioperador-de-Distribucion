
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.UnitTests;

public class MaquinaEstadosEnvioTests
{
    [Theory]
    [InlineData(EstadoEnvio.Admitido, DisparadorEnvio.RecibirEnDeposito, EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.EnDeposito, DisparadorEnvio.AsignarARuta, EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.EnDeposito, DisparadorEnvio.Reprogramar, EstadoEnvio.Reprogramado)]
    [InlineData(EstadoEnvio.AsignadoARuta, DisparadorEnvio.QuitarDeRuta, EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.AsignadoARuta, DisparadorEnvio.Despachar, EstadoEnvio.EnTransito)]
    [InlineData(EstadoEnvio.EnTransito, DisparadorEnvio.Entregar, EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.EnTransito, DisparadorEnvio.RegistrarIntentoFallido, EstadoEnvio.NoEntregado)]
    [InlineData(EstadoEnvio.NoEntregado, DisparadorEnvio.RetornarADeposito, EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.EnDeposito, DisparadorEnvio.IniciarDevolucion, EstadoEnvio.EnDevolucion)]
    [InlineData(EstadoEnvio.Reprogramado, DisparadorEnvio.AsignarARuta, EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.EnDevolucion, DisparadorEnvio.ConfirmarDevolucion, EstadoEnvio.Devuelto)]
    [InlineData(EstadoEnvio.Extraviado, DisparadorEnvio.Reaparecer, EstadoEnvio.EnDeposito)]
    public void TransicionValida_CambiaAlEstadoEsperado(
        EstadoEnvio inicial,
        DisparadorEnvio disparador,
        EstadoEnvio esperado)
    {
        var estado = inicial;

        var maquina = MaquinaEstadosEnvio.Crear(
            () => estado,
            nuevo => estado = nuevo,
            () => EstadoEnvio.EnDeposito);

        Assert.True(maquina.CanFire(disparador));

        maquina.Fire(disparador);

        Assert.Equal(esperado, estado);
    }

    [Theory]
    [InlineData(EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.EnTransito)]
    [InlineData(EstadoEnvio.NoEntregado)]
    [InlineData(EstadoEnvio.Reprogramado)]
    [InlineData(EstadoEnvio.EnDevolucion)]
    public void EstadoNoFinal_PermiteDeclararExtraviado(
        EstadoEnvio inicial)
    {
        var estado = inicial;

        var maquina = MaquinaEstadosEnvio.Crear(
            () => estado,
            nuevo => estado = nuevo);

        Assert.True(maquina.CanFire(DisparadorEnvio.DeclararExtraviado));

        maquina.Fire(DisparadorEnvio.DeclararExtraviado);

        Assert.Equal(EstadoEnvio.Extraviado, estado);
    }

    [Theory]
    [InlineData(EstadoEnvio.Admitido, DisparadorEnvio.Entregar)]
    [InlineData(EstadoEnvio.Admitido, DisparadorEnvio.AsignarARuta)]
    [InlineData(EstadoEnvio.EnDeposito, DisparadorEnvio.Entregar)]
    [InlineData(EstadoEnvio.AsignadoARuta, DisparadorEnvio.Entregar)]
    [InlineData(EstadoEnvio.EnTransito, DisparadorEnvio.RecibirEnDeposito)]
    [InlineData(EstadoEnvio.Entregado, DisparadorEnvio.Reprogramar)]
    [InlineData(EstadoEnvio.Devuelto, DisparadorEnvio.AsignarARuta)]
    [InlineData(EstadoEnvio.NoEntregado, DisparadorEnvio.IniciarDevolucion)]
    public void TransicionInvalida_EsRechazada(
        EstadoEnvio inicial,
        DisparadorEnvio disparador)
    {
        var estado = inicial;
        
        var maquina = MaquinaEstadosEnvio.Crear(
            () => estado,
            nuevo => estado = nuevo);

        Assert.False(maquina.CanFire(disparador));
        Assert.Throws<InvalidOperationException>(
            () => maquina.Fire(disparador));

        Assert.Equal(inicial, estado);

    }

    [Theory]
    [InlineData(EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.Devuelto)]
    public void EstadoFinal_NoPermiteNuevasTransiciones(
        EstadoEnvio estadoFinal)
    {
        var estado = estadoFinal;

        var maquina = MaquinaEstadosEnvio.Crear(
            () => estado,
            nuevo => estado = nuevo);

        foreach (var disparador in Enum.GetValues<DisparadorEnvio>())
        {
            Assert.False(maquina.CanFire(disparador));
        }
    }

    [Theory]
    [InlineData(EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.Reprogramado)]
    public void QuitarDeRuta_RestauraEstadoAnterior(
        EstadoEnvio estadoAnterior)
    {
        var estado = EstadoEnvio.AsignadoARuta;

        var maquina = MaquinaEstadosEnvio.Crear(
            () => estado,
            nuevo => estado = nuevo,
            () => estadoAnterior);

        maquina.Fire(DisparadorEnvio.QuitarDeRuta);

        Assert.Equal(estadoAnterior, estado);
    }
}
