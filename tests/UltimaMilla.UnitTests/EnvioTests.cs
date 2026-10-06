using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

public class EnvioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 8, 19, 0, 0, TimeSpan.FromHours(-3));
    private static readonly Destinatario Ana = new("Ana Pérez", "099123456");
    private static readonly Direccion Casa = new("Av. Italia", "1234", "Montevideo", "Montevideo");
    private static readonly NuevoBulto Caja = new(2.5m, 20, 30, 40);

    private static CuentaComercial NuevaCuenta() =>
        CuentaComercial.Crear(Guid.NewGuid(), operadorId: Guid.NewGuid(), comercioId: Guid.NewGuid());

    [Fact]
    public void Crear_envio_valido_queda_admitido_y_hereda_el_operador_de_la_cuenta()
    {
        var cuenta = NuevaCuenta();

        var envio = Envio.Crear(cuenta, "PED-001", Modalidad.Estandar, Ana, Casa, [Caja], Ahora);

        Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
        Assert.Equal(cuenta.OperadorId, envio.OperadorId);
        Assert.Equal(cuenta.Id, envio.CuentaComercialId);
        Assert.Equal(Ahora, envio.FechaAlta);
    }

    [Fact]
    public void Los_bultos_se_identifican_con_la_referencia_y_un_numero()
    {
        var envio = Envio.Crear(NuevaCuenta(), "PED-002", Modalidad.Urgente, Ana, Casa, [Caja, Caja], Ahora);

        Assert.Equal(new[] { "PED-002-1", "PED-002-2" }, envio.Bultos.Select(b => b.Identificacion));
        Assert.Equal(5.0m, envio.PesoTotalKg);
    }

    [Fact]
    public void Crear_sin_bultos_falla()
    {
        Assert.Throws<ArgumentException>(() =>
            Envio.Crear(NuevaCuenta(), "PED-003", Modalidad.Estandar, Ana, Casa, [], Ahora));
    }

    [Fact]
    public void Crear_con_cuenta_inactiva_falla()
    {
        var cuenta = NuevaCuenta();
        cuenta.Desactivar();

        Assert.Throws<InvalidOperationException>(() =>
            Envio.Crear(cuenta, "PED-004", Modalidad.Estandar, Ana, Casa, [Caja], Ahora));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Bulto_con_peso_no_positivo_falla(decimal peso)
    {
        Assert.Throws<ArgumentException>(() =>
            Envio.Crear(NuevaCuenta(), "PED-005", Modalidad.Estandar, Ana, Casa, [new NuevoBulto(peso, 10, 10, 10)], Ahora));
    }

    [Fact]
    public void Destinatario_sin_nombre_falla()
    {
        Assert.Throws<ArgumentException>(() => new Destinatario("  ", null));
    }
}
