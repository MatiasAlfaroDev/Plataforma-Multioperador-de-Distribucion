
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

        ruta.VerificarEditable();

        if (!ruta.HoraSalida.HasValue)
            throw new InvalidOperationException(
                "La hoja de ruta debe tener una hora de salida.");

        if (envios.Count == 0)
            throw new InvalidOperationException(
                "Debe seleccionarse al menos un envío.");

        if (envios.Any(x => x is null || x.Envio is null || x.Zona is null))
            throw new ArgumentException(
                "La selección contiene datos inválidos.");

        if (envios.Select(x => x.Envio.Id).Distinct().Count() != envios.Count)
            throw new InvalidOperationException(
                "Hay envíos repetidos en la selección.");

        var activas = ruta.Paradas
            .Where(p => p.Activa)
            .ToList();

        var idsSeleccionados = envios
            .Select(x => x.Envio.Id)
            .ToHashSet();

        // No permite recalcular omitiendo una parada existente.
        if (activas.Any(p => !idsSeleccionados.Contains(p.EnvioId)))
            throw new InvalidOperationException(
                "La planificación debe incluir todas las paradas activas.");

        if (envios.Count > ruta.MaximoParadas)
            throw new InvalidOperationException(
                "Se supera el máximo de paradas.");

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

            var yaAsignado = activas.Any(p => p.EnvioId == envio.Id);

            if (yaAsignado)
            {
                if (envio.Estado != EstadoEnvio.AsignadoARuta)
                    throw new InvalidOperationException(
                        "Una parada activa tiene un envío con estado incorrecto.");
            }
            else if (envio.Estado != EstadoEnvio.EnDeposito &&
                    envio.Estado != EstadoEnvio.Reprogramado)
            {
                throw new InvalidOperationException(
                    "El envío no está disponible para asignación.");
            }

            if (envio.FechaEntregaComprometida != ruta.Fecha)
                throw new InvalidOperationException(
                    "La fecha comprometida no coincide con la ruta.");

            if (!envio.HoraEntregaDesde.HasValue ||
                !envio.HoraEntregaHasta.HasValue)
                throw new InvalidOperationException(
                    "El envío no tiene una franja comprometida.");

            if (!zona.OfreceFranja(
                    envio.HoraEntregaDesde.Value,
                    envio.HoraEntregaHasta.Value))
                throw new InvalidOperationException(
                    "La zona no ofrece la franja comprometida.");

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

        var ordenados = envios
            .OrderBy(x => x.Envio.HoraEntregaHasta!.Value)
            .ThenBy(x => x.Envio.HoraEntregaDesde!.Value)
            .ThenBy(x => x.Envio.Direccion.Localidad)
            .ThenBy(x => x.Envio.ReferenciaExterna)
            .ToList();

        var paradas = ordenados
            .Select(item => new ParadaPlanificada(
                item.Envio.Id,
                item.Envio.HoraEntregaDesde!.Value,
                item.Envio.HoraEntregaHasta!.Value,
                item.Zona.MinutosTrasladoEstimado!.Value))
            .ToList();

        return PlanificadorHorarios.ValidarYCalcular(
            ruta.HoraSalida.Value,
            paradas);
    }
}
