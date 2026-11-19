using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.UnitTests;

public class RetiroVencidoTests
{
    private static readonly DateOnly Fecha = new(2026, 11, 16);

    private static Retiro CrearRetiro() => Retiro.Crear(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Producto fallado",
        new Direccion("Asamblea", "362", "San Jose de Mayo", "San José"),
        new Direccion("Comercio", "200", "San Jose de Mayo", "San José"),
        Fecha,
        new TimeOnly(9, 0),
        new TimeOnly(13, 0),
        [BultoRetiro.Crear("RET-001", 5m, 20, 20, 20)],
        new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Retiro_vencido_pasa_a_la_fecha_de_la_hoja_y_se_asigna()
    {
        var retiro = CrearRetiro();
        var ahora = new DateTime(2026, 11, 18, 21, 0, 0);

        Assert.True(retiro.Vencido(ahora));
        retiro.MoverAFecha(new DateOnly(2026, 11, 19), ahora);
        retiro.AsignarARuta(Guid.NewGuid());

        Assert.Equal(new DateOnly(2026, 11, 19), retiro.FechaSolicitada);
        Assert.Equal(EstadoRetiro.AsignadoARuta, retiro.Estado);
    }

    [Fact]
    public void Retiro_dentro_de_su_franja_no_cambia_de_fecha()
    {
        var retiro = CrearRetiro();
        var ahora = Fecha.ToDateTime(new TimeOnly(12, 59));

        Assert.False(retiro.Vencido(ahora));
        Assert.Throws<InvalidOperationException>(() =>
            retiro.MoverAFecha(Fecha.AddDays(1), ahora));
    }
}
