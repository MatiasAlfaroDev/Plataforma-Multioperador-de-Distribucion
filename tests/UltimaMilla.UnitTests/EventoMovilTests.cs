using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

public class EventoMovilTests
{
    private static readonly DateTimeOffset Alta = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ahora = Alta.AddHours(5);

    private static Envio NuevoEnvio() => Envio.Crear(
        CuentaComercial.Crear(Guid.NewGuid(), operadorId: Guid.NewGuid(), comercioId: Guid.NewGuid()),
        "PED-100", Modalidad.Estandar,
        new Destinatario("Ana Pérez", null),
        new Direccion("Av. Italia", "1234", "Montevideo", "Montevideo"),
        [new NuevoBulto(1m, 10, 10, 10)],
        Alta);

    [Fact]
    public void Entrega_valida_conserva_el_id_del_telefono_y_la_hora_de_la_calle()
    {
        var envio = NuevoEnvio();
        var idDelTelefono = Guid.NewGuid();
        var enLaCalle = Alta.AddHours(2);

        var evento = EventoMovil.Registrar(idDelTelefono, envio, TipoEventoMovil.Entregado, null, "Ana", enLaCalle, Ahora);

        Assert.Equal(idDelTelefono, evento.Id);
        Assert.Equal(envio.OperadorId, evento.OperadorId);
        Assert.Equal(enLaCalle, evento.OcurridoEn);
        Assert.Equal(Ahora, evento.RecibidoEn);
    }

    [Fact]
    public void Entrega_sin_receptor_falla()
    {
        Assert.Throws<ArgumentException>(() =>
            EventoMovil.Registrar(Guid.NewGuid(), NuevoEnvio(), TipoEventoMovil.Entregado, null, " ", Alta.AddHours(1), Ahora));
    }

    [Fact]
    public void Intento_fallido_sin_motivo_falla()
    {
        Assert.Throws<ArgumentException>(() =>
            EventoMovil.Registrar(Guid.NewGuid(), NuevoEnvio(), TipoEventoMovil.NoEntregado, null, null, Alta.AddHours(1), Ahora));
    }

    [Fact]
    public void Evento_sin_id_del_telefono_falla()
    {
        Assert.Throws<ArgumentException>(() =>
            EventoMovil.Registrar(Guid.Empty, NuevoEnvio(), TipoEventoMovil.Entregado, null, "Ana", Alta.AddHours(1), Ahora));
    }

    [Fact]
    public void Evento_con_fecha_en_el_futuro_falla()
    {
        Assert.Throws<ArgumentException>(() =>
            EventoMovil.Registrar(Guid.NewGuid(), NuevoEnvio(), TipoEventoMovil.Entregado, null, "Ana", Ahora.AddHours(1), Ahora));
    }
}
