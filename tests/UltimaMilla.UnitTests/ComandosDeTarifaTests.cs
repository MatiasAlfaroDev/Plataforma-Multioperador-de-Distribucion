using UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;
using UltimaMilla.Application.Configuracion.Commands.CrearTarifa;

namespace UltimaMilla.UnitTests;

public class ComandosDeTarifaTests
{
    private static readonly Guid Operador = Guid.NewGuid();
    private static readonly Guid Zona = Guid.NewGuid();

    [Fact]
    public void El_comando_de_alta_conserva_todos_los_datos_de_la_tarifa()
    {
        var comando = new CrearTarifaCommand(
            Operador, Zona, "Cuadro 2026", "RetiroDomicilio", 100m, 10m, 50m, 20m, 5m);

        Assert.Equal(Operador, comando.OperadorId);
        Assert.Equal(Zona, comando.ZonaCoberturaId);
        Assert.Equal("Cuadro 2026", comando.Nombre);
        Assert.Equal("RetiroDomicilio", comando.TipoServicio);
        Assert.Equal(100m, comando.PrecioBase);
        Assert.Equal(10m, comando.PrecioPorKg);
        Assert.Equal(50m, comando.PrecioPorMetroCubico);
        Assert.Equal(20m, comando.RecargoUrgentePorcentaje);
        Assert.Equal(5m, comando.BonificacionPorcentaje);
    }

    [Fact]
    public void El_comando_de_actualizacion_usa_el_servicio_de_entrega_por_defecto()
    {
        var cuadro = Guid.NewGuid();

        var comando = new ActualizarTarifaCommand(Operador, cuadro, Zona, 120m, 12m, 60m, 25m, 10m);

        Assert.Equal(Operador, comando.OperadorId);
        Assert.Equal(cuadro, comando.CuadroTarifarioId);
        Assert.Equal(Zona, comando.ZonaCoberturaId);
        Assert.Equal(120m, comando.PrecioBase);
        Assert.Equal(12m, comando.PrecioPorKg);
        Assert.Equal(60m, comando.PrecioPorMetroCubico);
        Assert.Equal(25m, comando.RecargoUrgentePorcentaje);
        Assert.Equal(10m, comando.BonificacionPorcentaje);
        Assert.Equal("Entrega", comando.TipoServicio);
    }

    [Fact]
    public void El_comando_de_actualizacion_admite_otro_tipo_de_servicio()
    {
        var comando = new ActualizarTarifaCommand(
            Operador, Guid.NewGuid(), Zona, 1m, 1m, 1m, 0m, 0m, "RetiroDomicilio");

        Assert.Equal("RetiroDomicilio", comando.TipoServicio);
    }
}
