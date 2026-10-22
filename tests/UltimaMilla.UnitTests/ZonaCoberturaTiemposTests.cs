
using UltimaMilla.Domain.Configuracion;
using Xunit;

namespace UltimaMilla.UnitTests;

public class ZonaCoberturaTiemposTests
{
    [Fact]
    public void ConfigurarTiempoEstimado_GuardaValorCorrectamente()
    {
        var zona = ZonaCobertura.Crear(
            Guid.NewGuid(),
            "Centro",
            "San José");

        zona.ConfigurarTiempoEstimado(15);

        Assert.Equal(15, zona.MinutosTrasladoEstimado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-15)]
    public void ConfigurarTiempoEstimado_RechazaValoresInvalidos(
        int minutos)
    {
        var zona = ZonaCobertura.Crear(
            Guid.NewGuid(),
            "Centro",
            "San José");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            zona.ConfigurarTiempoEstimado(minutos));
    }
}
