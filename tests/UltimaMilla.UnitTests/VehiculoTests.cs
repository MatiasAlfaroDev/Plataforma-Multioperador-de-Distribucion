using UltimaMilla.Domain.Organizacion;
using Xunit;

namespace UltimaMilla.UnitTests;

public class VehiculoTests
{
    [Fact]
    public void Crear_normaliza_la_matricula_y_queda_activo()
    {
        var vehiculo = Vehiculo.Crear(Guid.NewGuid(), "  stp1234 ", 500m, 12m);

        Assert.Equal("STP1234", vehiculo.Matricula);
        Assert.Equal(500m, vehiculo.CapacidadPesoKg);
        Assert.Equal(12m, vehiculo.CapacidadVolumenM3);
        Assert.True(vehiculo.Activo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_rechaza_matricula_vacia(string matricula)
    {
        Assert.ThrowsAny<ArgumentException>(() => Vehiculo.Crear(Guid.NewGuid(), matricula, 500m, 12m));
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(-1, 12)]
    [InlineData(500, 0)]
    [InlineData(500, -3)]
    public void Crear_rechaza_capacidades_que_no_son_positivas(int pesoKg, int volumenM3)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Vehiculo.Crear(Guid.NewGuid(), "STP1234", pesoKg, volumenM3));
    }

    [Fact]
    public void Se_puede_dar_de_baja_y_reactivar()
    {
        var vehiculo = Vehiculo.Crear(Guid.NewGuid(), "STP1234", 500m, 12m);

        vehiculo.Desactivar();
        Assert.False(vehiculo.Activo);

        vehiculo.Activar();
        Assert.True(vehiculo.Activo);
    }
}
