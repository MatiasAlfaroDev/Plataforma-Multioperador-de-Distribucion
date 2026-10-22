
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Domain.Rutas;

public sealed record EnvioParaPlanificar(
    Envio Envio,
    ZonaCobertura Zona);

public static class PlanificacionHojaDeRuta
{
    public static IReadOnlyList<HorarioParada> Calcular(
        HojaDeRuta ruta,
        IReadOnlyList<EnvioParaPlanificar> envios)
    {
        ArgumentNullException.ThrowIfNull(ruta);
        ArgumentNullException.ThrowIfNull(envios);

        if (!ruta.HoraSalida.HasValue)
            throw new InvalidOperationException(
                "La hoja de ruta debe tener una hora de salida.");

        if (envios.Count == 0)
            throw new InvalidOperationException(
                "Debe seleccionarse al menos un envío.");

        if (envios.Any(x => x is null))
            throw new ArgumentException(
                "La selección contiene datos inválidos.");

        if (envios.Select(x => x.Envio.Id).Distinct().Count()
            != envios.Count)
            throw new InvalidOperationException(
                "Hay envíos repetidos en la selección.");

        var paradasActivas = ruta.Paradas
            .Count(x => x.Activa);

        if (paradasActivas + envios.Count > ruta.MaximoParadas)
            throw new InvalidOperationException(
                "Se supera el máximo de paradas.");

        // Este cálculo inicial planifica rutas sin paradas
        // anteriores. La ampliación de rutas requiere
        // recalcular todas las paradas activas.
        if (paradasActivas > 0)
            throw new InvalidOperationException(
                "La ruta ya tiene paradas. Debe recalcularse "
                + "su planificación completa.");

        decimal pesoTotal = 0;
        decimal volumenTotal = 0;

        foreach (var item in envios)
        {
            var envio = item.Envio;
            var zona = item.Zona;

            if (envio.OperadorId != ruta.OperadorId ||
                zona.OperadorId != ruta.OperadorId)
                throw new InvalidOperationException(
                    "Los datos pertenecen a otro operador.");

            if (!zona.Activa)
                throw new InvalidOperationException(
                    "La zona de cobertura está inactiva.");

            if (envio.Estado != EstadoEnvio.EnDeposito &&
                envio.Estado != EstadoEnvio.Reprogramado)
                throw new InvalidOperationException(
                    "El envío no está disponible para asignación.");

            if (envio.Estado == EstadoEnvio.Reprogramado)
            {
                if (!envio.FechaReprogramada.HasValue ||
                    DateOnly.FromDateTime(
                        envio.FechaReprogramada.Value.Date) != ruta.Fecha)
                    throw new InvalidOperationException(
                        "La ruta no coincide con la reprogramación.");
            }
            else if (envio.FechaEntregaComprometida != ruta.Fecha)
            {
                throw new InvalidOperationException(
                    "La fecha comprometida no coincide con la ruta.");
            }

            if (!envio.HoraEntregaDesde.HasValue ||
                !envio.HoraEntregaHasta.HasValue)
                throw new InvalidOperationException(
                    "El envío no tiene una franja comprometida.");

            if (zona.MinutosTrasladoEstimado is null or <= 0)
                throw new InvalidOperationException(
                    "La zona no tiene un tiempo entre paradas válido.");

            pesoTotal += envio.PesoTotalKg;
            volumenTotal += envio.VolumenTotalMetrosCubicos;
        }

        if (pesoTotal > ruta.CapacidadPesoKg)
            throw new InvalidOperationException(
                "Se supera la capacidad de peso del vehículo.");

        if (volumenTotal > ruta.CapacidadVolumenM3)
            throw new InvalidOperationException(
                "Se supera la capacidad de volumen del vehículo.");

        // Criterio documentable:
        // primero los compromisos que vencen antes.
        // Para empates, priorizar el inicio de franja.
        var ordenados = envios
            .OrderBy(x => x.Envio.HoraEntregaHasta!.Value)
            .ThenBy(x => x.Envio.HoraEntregaDesde!.Value)
            .ThenBy(x => x.Envio.Direccion.Localidad)
            .ThenBy(x => x.Envio.ReferenciaExterna)
            .ToList();

        var paradas = ordenados.Select(item =>
            new ParadaPlanificada(
                item.Envio.Id,
                item.Envio.HoraEntregaDesde!.Value,
                item.Envio.HoraEntregaHasta!.Value,
                item.Zona.MinutosTrasladoEstimado!.Value
            )).ToList();

        return PlanificadorHorarios.ValidarYCalcular(
            ruta.HoraSalida.Value,
            paradas);
    }
}
