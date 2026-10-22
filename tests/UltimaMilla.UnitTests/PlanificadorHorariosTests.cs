
using UltimaMilla.Domain.Rutas;
using Xunit;

namespace UltimaMilla.UnitTests;

public class PlanificadorHorariosTests
{
    [Fact]
    public void AcumulaTiempoSegunOrdenDeParadas()
    {
        var paradas = Enumerable.Range(1, 3)
            .Select(_ => new ParadaPlanificada(
                Guid.NewGuid(),
                new TimeOnly(9, 0),
                new TimeOnly(12, 0),
                15))
            .ToList();

        var resultado = PlanificadorHorarios.ValidarYCalcular(
            new TimeOnly(9, 0),
            paradas);

        Assert.Equal(new TimeOnly(9, 15),
            resultado[0].HoraEntregaEstimada);

        Assert.Equal(new TimeOnly(9, 30),
            resultado[1].HoraEntregaEstimada);

        Assert.Equal(new TimeOnly(9, 45),
            resultado[2].HoraEntregaEstimada);
    }

    [Fact]
    public void EsperaCuandoLlegaAntesDeLaFranja()
    {
        var paradas = new[]
        {
            new ParadaPlanificada(
                Guid.NewGuid(),
                new TimeOnly(10, 0),
                new TimeOnly(12, 0),
                15)
        };

        var resultado = PlanificadorHorarios.ValidarYCalcular(
            new TimeOnly(9, 0),
            paradas);

        Assert.Equal(new TimeOnly(9, 15),
            resultado[0].HoraLlegada);

        Assert.Equal(new TimeOnly(10, 0),
            resultado[0].HoraEntregaEstimada);
    }

    [Fact]
    public void RechazaLlegadaFueraDeLaFranja()
    {
        var paradas = new[]
        {
            new ParadaPlanificada(
                Guid.NewGuid(),
                new TimeOnly(9, 0),
                new TimeOnly(9, 30),
                45)
        };

        Assert.Throws<InvalidOperationException>(() =>
            PlanificadorHorarios.ValidarYCalcular(
                new TimeOnly(9, 0),
                paradas));
    }
}
