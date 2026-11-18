using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Application.CuentasComerciales.Queries.ObtenerCuentaCorriente;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.UnitTests;

/// <summary>Cuenta corriente y liquidaciones del comercio (letra 4, Portal del comercio).</summary>
public class CuentaCorrienteTests
{
    private static readonly Guid CuentaA = Guid.NewGuid();
    private static readonly Guid CuentaB = Guid.NewGuid();
    private static readonly DateOnly Desde = new(2026, 10, 1);
    private static readonly DateOnly Hasta = new(2026, 11, 30);

    private static CuentaComercialDto Cuenta(Guid id, string operador) =>
        new(id, Guid.NewGuid(), operador, Guid.NewGuid(), "Comercio", true, EstadoCuentaComercial.Confirmada);

    private static CargoCuenta Cargo(Guid cuenta, int anio, int mes, int dia, decimal? importe, string tipo = "Envio") =>
        new(cuenta, new DateTimeOffset(anio, mes, dia, 12, 0, 0, TimeSpan.Zero), tipo, Guid.NewGuid(), $"REF-{dia}", "Entregado", importe);

    [Fact]
    public void SaldoAnterior_Cargos_Y_SaldoFinal()
    {
        var cargos = new[]
        {
            Cargo(CuentaA, 2026, 9, 15, 100m),   // antes del período
            Cargo(CuentaA, 2026, 10, 5, 200m),
            Cargo(CuentaA, 2026, 11, 20, 50m, "Retiro")
        };

        var resultado = CalculadoraCuentaCorriente.Calcular([Cuenta(CuentaA, "Operador A")], cargos, Desde, Hasta);

        var cuenta = Assert.Single(resultado.Cuentas);
        Assert.Equal(100m, cuenta.SaldoAnterior);
        Assert.Equal(250m, cuenta.CargosDelPeriodo);
        Assert.Equal(350m, cuenta.SaldoFinal);
        Assert.Equal(2, cuenta.Movimientos.Count);
        Assert.Equal(300m, cuenta.Movimientos[0].SaldoAcumulado);
        Assert.Equal(350m, cuenta.Movimientos[1].SaldoAcumulado);
    }

    [Fact]
    public void Liquidaciones_PorMes_SeparanEnviosYRetiros()
    {
        var cargos = new[]
        {
            Cargo(CuentaA, 2026, 10, 1, 100m),
            Cargo(CuentaA, 2026, 10, 31, 40m, "Retiro"),
            Cargo(CuentaA, 2026, 11, 2, 60m)
        };

        var cuenta = Assert.Single(CalculadoraCuentaCorriente.Calcular([Cuenta(CuentaA, "A")], cargos, Desde, Hasta).Cuentas);

        Assert.Collection(cuenta.Liquidaciones,
            octubre =>
            {
                Assert.Equal((2026, 10), (octubre.Anio, octubre.Mes));
                Assert.Equal(1, octubre.Envios);
                Assert.Equal(1, octubre.Retiros);
                Assert.Equal(140m, octubre.Total);
            },
            noviembre =>
            {
                Assert.Equal((2026, 11), (noviembre.Anio, noviembre.Mes));
                Assert.Equal(60m, noviembre.Total);
            });
    }

    [Fact]
    public void CadaCuenta_SoloSumaSusPropiosCargos()
    {
        var cargos = new[]
        {
            Cargo(CuentaA, 2026, 10, 10, 100m),
            Cargo(CuentaB, 2026, 10, 10, 999m),
            Cargo(Guid.NewGuid(), 2026, 10, 10, 5000m) // de una cuenta que no es del comercio
        };

        var resultado = CalculadoraCuentaCorriente.Calcular(
            [Cuenta(CuentaA, "A"), Cuenta(CuentaB, "B")], cargos, Desde, Hasta);

        Assert.Equal(2, resultado.Cuentas.Count);
        Assert.Equal(100m, resultado.Cuentas.Single(c => c.CuentaComercialId == CuentaA).SaldoFinal);
        Assert.Equal(999m, resultado.Cuentas.Single(c => c.CuentaComercialId == CuentaB).SaldoFinal);
    }

    [Fact]
    public void ServicioSinTarifa_NoSumaPeroSeInforma()
    {
        var cargos = new[] { Cargo(CuentaA, 2026, 10, 10, 100m), Cargo(CuentaA, 2026, 10, 11, null) };

        var cuenta = Assert.Single(CalculadoraCuentaCorriente.Calcular([Cuenta(CuentaA, "A")], cargos, Desde, Hasta).Cuentas);

        Assert.Equal(100m, cuenta.SaldoFinal);
        Assert.Equal(1, cuenta.ServiciosSinTarifa);
        Assert.Equal(2, cuenta.Movimientos.Count);
    }

    [Fact]
    public void ElUltimoDiaDelPeriodo_EntraCompleto()
    {
        var cargos = new[]
        {
            new CargoCuenta(CuentaA, new DateTimeOffset(2026, 11, 30, 23, 59, 0, TimeSpan.Zero), "Envio", Guid.NewGuid(), "R1", "Admitido", 10m),
            new CargoCuenta(CuentaA, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), "Envio", Guid.NewGuid(), "R2", "Admitido", 20m)
        };

        var cuenta = Assert.Single(CalculadoraCuentaCorriente.Calcular([Cuenta(CuentaA, "A")], cargos, Desde, Hasta).Cuentas);

        Assert.Equal(10m, cuenta.CargosDelPeriodo);
        Assert.Single(cuenta.Movimientos);
    }

    [Fact]
    public void UnServicioRepetido_SeCobraUnaVez()
    {
        var cargo = Cargo(CuentaA, 2026, 10, 10, 100m);

        var cuenta = Assert.Single(CalculadoraCuentaCorriente.Calcular([Cuenta(CuentaA, "A")], [cargo, cargo], Desde, Hasta).Cuentas);

        Assert.Equal(100m, cuenta.SaldoFinal);
    }
}
