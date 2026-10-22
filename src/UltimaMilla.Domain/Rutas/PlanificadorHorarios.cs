
namespace UltimaMilla.Domain.Rutas;

public sealed record ParadaPlanificada(
    Guid EnvioId,
    TimeOnly HoraDesde,
    TimeOnly HoraHasta,
    int MinutosTraslado);

public sealed record HorarioParada(
    Guid EnvioId,
    TimeOnly HoraLlegada,
    TimeOnly HoraEntregaEstimada);

public static class PlanificadorHorarios
{
    public static IReadOnlyList<HorarioParada> ValidarYCalcular(
        TimeOnly horaSalida,
        IReadOnlyList<ParadaPlanificada> paradas)
    {
        ArgumentNullException.ThrowIfNull(paradas);

        var resultado = new List<HorarioParada>();

        var minutoActual =
            horaSalida.Hour * 60 + horaSalida.Minute;

        foreach (var parada in paradas)
        {
            if (parada.MinutosTraslado <= 0)
                throw new ArgumentException(
                    "El tiempo entre paradas debe ser positivo.");

            if (parada.HoraDesde >= parada.HoraHasta)
                throw new ArgumentException(
                    "La franja horaria es inválida.");

            var llegada = minutoActual + parada.MinutosTraslado;

            var inicioFranja =
                parada.HoraDesde.Hour * 60 +
                parada.HoraDesde.Minute;

            var finFranja =
                parada.HoraHasta.Hour * 60 +
                parada.HoraHasta.Minute;

            // Si llega antes, debe esperar hasta que
            // comience la franja comprometida.
            var entregaEstimada = Math.Max(
                llegada,
                inicioFranja);

            if (entregaEstimada >= finFranja ||
                entregaEstimada >= 24 * 60)
            {
                throw new InvalidOperationException(
                    $"No se puede cumplir la franja del envío {parada.EnvioId}.");
            }

            resultado.Add(new HorarioParada(
                parada.EnvioId,
                ConvertirHora(llegada),
                ConvertirHora(entregaEstimada)));

            // El siguiente tramo comienza en el horario
            // estimado de esta entrega.
            minutoActual = entregaEstimada;
        }

        return resultado;
    }

    private static TimeOnly ConvertirHora(int minutos)
    {
        if (minutos < 0 || minutos >= 24 * 60)
            throw new InvalidOperationException(
                "El horario calculado supera el día de operación.");

        return new TimeOnly(
            minutos / 60,
            minutos % 60);
    }
}
