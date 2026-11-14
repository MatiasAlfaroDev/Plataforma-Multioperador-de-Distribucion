using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;
using Xunit;

namespace UltimaMilla.UnitTests;

public class RetiroCancelacionYDesasignacionTests
{
    private static readonly Guid OperadorId = Guid.NewGuid();
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);

    private static Retiro Solicitado() =>
        Retiro.Crear(
            OperadorId,
            Guid.NewGuid(),
            "Cambio de talle",
            new Direccion("Origen", "100", "San José", "San José"),
            new Direccion("Comercio", "200", "San José", "San José"),
            Hoy,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [BultoRetiro.Crear("RET-001", 5m, 20, 20, 20)],
            DateTimeOffset.UtcNow);

    private static HojaDeRuta Hoja() =>
        HojaDeRuta.Crear(
            OperadorId,
            Repartidor.Crear(OperadorId, "Repartidor", "099123456"),
            Vehiculo.Crear(OperadorId, "STP1234", 100m, 10m),
            Hoy,
            5);

    [Fact]
    public void Cancelar_un_solicitado_guarda_el_motivo()
    {
        var retiro = Solicitado();

        retiro.Cancelar("  El cliente se quedó con el producto  ");

        Assert.Equal(EstadoRetiro.Cancelado, retiro.Estado);
        Assert.Equal("El cliente se quedó con el producto", retiro.MotivoCancelacion);
    }

    [Fact]
    public void Cancelar_exige_motivo()
    {
        var retiro = Solicitado();

        Assert.Throws<ArgumentException>(() => retiro.Cancelar(" "));
        Assert.Throws<ArgumentException>(() => retiro.Cancelar(new string('x', 1001)));
        Assert.Equal(EstadoRetiro.Solicitado, retiro.Estado);
    }

    [Fact]
    public void No_se_cancela_un_retiro_asignado_ni_se_reactiva_uno_cancelado()
    {
        var asignado = Solicitado();
        asignado.AsignarARuta(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => asignado.Cancelar("Motivo"));

        var cancelado = Solicitado();
        cancelado.Cancelar("Motivo");
        Assert.Throws<InvalidOperationException>(() => cancelado.AsignarARuta(Guid.NewGuid()));
    }

    [Fact]
    public void Quitar_de_la_hoja_libera_la_parada_y_vuelve_a_solicitado()
    {
        var hoja = Hoja();
        var retiro = Solicitado();
        hoja.AgregarRetiro(retiro);
        retiro.AsignarARuta(hoja.Id);

        hoja.QuitarRetiro(retiro, DateTimeOffset.UtcNow);

        Assert.Equal(EstadoRetiro.Solicitado, retiro.Estado);
        Assert.Null(retiro.HojaDeRutaId);
        Assert.False(Assert.Single(hoja.Paradas).Activa);
    }

    [Fact]
    public void No_se_quita_un_retiro_de_una_hoja_despachada()
    {
        var hoja = Hoja();
        var retiro = Solicitado();
        hoja.AgregarRetiro(retiro);
        retiro.AsignarARuta(hoja.Id);
        hoja.MarcarDespachada(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => hoja.QuitarRetiro(retiro, DateTimeOffset.UtcNow));
        Assert.Equal(EstadoRetiro.AsignadoARuta, retiro.Estado);
    }

    [Fact]
    public void Desasignar_solo_desde_asignado()
    {
        Assert.Throws<InvalidOperationException>(() => Solicitado().Desasignar());
    }
}
