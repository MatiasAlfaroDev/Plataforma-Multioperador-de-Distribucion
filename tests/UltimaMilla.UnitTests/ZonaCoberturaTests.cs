
using UltimaMilla.Domain.Configuracion;
using Xunit;

namespace UltimaMilla.UnitTests;

public class ZonaCoberturaTests
{
    [Fact]
    public void UnaZonaPuedeCubrirVariasLocalidades()
    {
        var zona = CrearZona();

        zona.AgregarLocalidad("Localidad A");
        zona.AgregarLocalidad("Localidad B");

        Assert.True(zona.CubreDireccion("Localidad A", null));
        Assert.True(zona.CubreDireccion("Localidad B", null));
        Assert.False(zona.CubreDireccion("Localidad C", null));
    }

    [Fact]
    public void UnaZonaPuedeCubrirUnSectorDeUnaCiudad()
    {
        var zona = CrearZona();

        zona.AgregarSector("Montevideo", "Cordón");

        Assert.True(zona.CubreDireccion("Montevideo", "Cordón"));
        Assert.False(zona.CubreDireccion("Montevideo", "Pocitos"));
        Assert.False(zona.CubreDireccion("Montevideo", null));
    }

    [Fact]
    public void UnaLocalidadCompletaNoPuedeDuplicarse()
    {
        var zona = CrearZona();

        zona.AgregarLocalidad("San José de Mayo");

        Assert.Throws<InvalidOperationException>(() =>
            zona.AgregarLocalidad("san josé de mayo"));
    }

    [Fact]
    public void NoPuedeAgregarseUnSectorSiYaCubreTodaLaLocalidad()
    {
        var zona = CrearZona();

        zona.AgregarLocalidad("Montevideo");

        Assert.Throws<InvalidOperationException>(() =>
            zona.AgregarSector("Montevideo", "Centro"));
    }

    [Fact]
    public void UnaZonaPuedeOfrecerFranjasDiferentes()
    {
        var zona = CrearZona();

        zona.AgregarFranja(new TimeOnly(9, 0), new TimeOnly(12, 0));
        zona.AgregarFranja(new TimeOnly(14, 0), new TimeOnly(18, 0));

        Assert.True(zona.OfreceFranja(
            new TimeOnly(9, 0),
            new TimeOnly(12, 0)));

        Assert.False(zona.OfreceFranja(
            new TimeOnly(12, 0),
            new TimeOnly(14, 0)));
    }

    [Fact]
    public void NoPermiteFranjasSuperpuestas()
    {
        var zona = CrearZona();

        zona.AgregarFranja(new TimeOnly(9, 0), new TimeOnly(12, 0));

        Assert.Throws<InvalidOperationException>(() =>
            zona.AgregarFranja(
                new TimeOnly(11, 0),
                new TimeOnly(14, 0)));
    }

    [Fact]
    public void UnaZonaInactivaNoOfreceCobertura()
    {
        var zona = CrearZona();

        zona.AgregarLocalidad("San José de Mayo");
        zona.AgregarFranja(new TimeOnly(9, 0), new TimeOnly(12, 0));
        zona.Desactivar();

        Assert.False(zona.CubreDireccion("San José de Mayo", null));
        Assert.False(zona.OfreceFranja(
            new TimeOnly(9, 0),
            new TimeOnly(12, 0)));
    }

    private static ZonaCobertura CrearZona()
    {
        return ZonaCobertura.Crear(
            Guid.NewGuid(),
            "Zona de prueba",
            "San José");
    }
}
