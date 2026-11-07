
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Domain.Rutas;

public sealed record EnvioParaPlanificar(
    Envio Envio,
    ZonaCobertura Zona);

public sealed record RetiroParaPlanificar(
    Retiro Retiro,
    ZonaCobertura Zona);

public enum TipoOperacionRuta
{
    Entrega,
    Retiro
}

public sealed record HorarioOperacionRuta(
    Guid ReferenciaId,
    TipoOperacionRuta Tipo,
    TimeOnly HoraLlegada,
    TimeOnly HoraOperacionEstimada,
    decimal PesoPosteriorKg,
    decimal VolumenPosteriorM3);

public static class PlanificacionHojaDeRuta
{
    private sealed record Operacion(
        Guid Id,
        TipoOperacionRuta Tipo,
        TimeOnly Desde,
        TimeOnly Hasta,
        int MinutosTraslado,
        string Localidad,
        decimal PesoKg,
        decimal VolumenM3);

    // Compatibilidad con los handlers actuales de entregas.
    public static IReadOnlyList<HorarioParada> Calcular(
        HojaDeRuta ruta,
        IReadOnlyList<EnvioParaPlanificar> envios)
    {
        var resultado = Calcular(
            ruta,
            envios,
            Array.Empty<RetiroParaPlanificar>());

        return resultado.Select(x => new HorarioParada(
            x.ReferenciaId,
            x.HoraLlegada,
            x.HoraOperacionEstimada)).ToList();
    }

    // Planificación unificada de entregas y retiros.
    public static IReadOnlyList<HorarioOperacionRuta> Calcular(
        HojaDeRuta ruta,
        IReadOnlyList<EnvioParaPlanificar> envios,
        IReadOnlyList<RetiroParaPlanificar> retiros)
    {
        ArgumentNullException.ThrowIfNull(ruta);
        ArgumentNullException.ThrowIfNull(envios);
        ArgumentNullException.ThrowIfNull(retiros);

        ruta.VerificarEditable();

        if (ruta.HoraSalida is null)
            throw new InvalidOperationException(
                "La hoja debe tener hora de salida.");

        var operaciones = new List<Operacion>();

        foreach (var item in envios)
        {
            if (item is null ||
                item.Envio is null ||
                item.Zona is null)
                throw new ArgumentException(
                    "Hay envíos inválidos.");

            var envio = item.Envio;
            var zona = item.Zona;

            ValidarZona(
                ruta,
                zona,
                envio.Direccion.Localidad);

            if (envio.OperadorId != ruta.OperadorId)
                throw new InvalidOperationException(
                    "El envío pertenece a otro operador.");

            bool asignado = ruta.Paradas.Any(p =>
                p.Activa && p.EnvioId == envio.Id);

            if (asignado)
            {
                if (envio.Estado != EstadoEnvio.AsignadoARuta)
                    throw new InvalidOperationException(
                        "El envío asignado tiene un estado inválido.");
            }
            else if (envio.Estado != EstadoEnvio.EnDeposito &&
                     envio.Estado != EstadoEnvio.Reprogramado)
            {
                throw new InvalidOperationException(
                    "El envío no está disponible para asignación.");
            }

            if (envio.FechaEntregaComprometida != ruta.Fecha ||
                envio.HoraEntregaDesde is null ||
                envio.HoraEntregaHasta is null)
                throw new InvalidOperationException(
                    "El compromiso del envío no coincide con la ruta.");

            ValidarFranja(
                zona,
                envio.HoraEntregaDesde.Value,
                envio.HoraEntregaHasta.Value);

            operaciones.Add(new Operacion(
                envio.Id,
                TipoOperacionRuta.Entrega,
                envio.HoraEntregaDesde.Value,
                envio.HoraEntregaHasta.Value,
                zona.MinutosTrasladoEstimado!.Value,
                envio.Direccion.Localidad,
                envio.PesoPendienteKg,
                envio.VolumenPendienteMetrosCubicos));
        }

        foreach (var item in retiros)
        {
            if (item is null ||
                item.Retiro is null ||
                item.Zona is null)
                throw new ArgumentException(
                    "Hay retiros inválidos.");

            var retiro = item.Retiro;
            var zona = item.Zona;

            ValidarZona(
                ruta,
                zona,
                retiro.DomicilioRetiro.Localidad);

            if (retiro.OperadorId != ruta.OperadorId)
                throw new InvalidOperationException(
                    "El retiro pertenece a otro operador.");

            bool asignado = ruta.Paradas.Any(p =>
                p.Activa && p.RetiroId == retiro.Id);

            if (asignado)
            {
                if (retiro.Estado != EstadoRetiro.AsignadoARuta ||
                    retiro.HojaDeRutaId != ruta.Id)
                    throw new InvalidOperationException(
                        "El retiro asignado tiene un estado inválido.");
            }
            else if (retiro.Estado != EstadoRetiro.Solicitado ||
                     retiro.HojaDeRutaId.HasValue)
            {
                throw new InvalidOperationException(
                    "El retiro no está disponible para asignación.");
            }

            if (retiro.FechaSolicitada != ruta.Fecha)
                throw new InvalidOperationException(
                    "La fecha del retiro no coincide con la ruta.");

            ValidarFranja(
                zona,
                retiro.HoraDesde,
                retiro.HoraHasta);

            operaciones.Add(new Operacion(
                retiro.Id,
                TipoOperacionRuta.Retiro,
                retiro.HoraDesde,
                retiro.HoraHasta,
                zona.MinutosTrasladoEstimado!.Value,
                retiro.DomicilioRetiro.Localidad,
                retiro.PesoTotalKg,
                retiro.VolumenTotalMetrosCubicos));
        }

        if (operaciones.Count == 0)
            throw new InvalidOperationException(
                "La ruta debe tener al menos una operación.");

        if (operaciones.Count > ruta.MaximoParadas)
            throw new InvalidOperationException(
                "Se supera el máximo de paradas.");

        // Los identificadores de las operaciones deben ser únicos.
        if (operaciones.Select(x => x.Id).Distinct().Count()
            != operaciones.Count)
            throw new InvalidOperationException(
                "Hay operaciones repetidas en la ruta.");

        // No se pueden omitir paradas activas.
        foreach (var parada in ruta.Paradas.Where(p => p.Activa))
        {
            bool incluida = operaciones.Any(x =>
                (x.Tipo == TipoOperacionRuta.Entrega &&
                 parada.EnvioId == x.Id) ||
                (x.Tipo == TipoOperacionRuta.Retiro &&
                 parada.RetiroId == x.Id));

            if (!incluida)
                throw new InvalidOperationException(
                    "La planificación debe incluir todas las paradas activas.");
        }

        // La carga inicial corresponde solamente a las entregas.
        decimal pesoActual = operaciones
            .Where(x => x.Tipo == TipoOperacionRuta.Entrega)
            .Sum(x => x.PesoKg);

        decimal volumenActual = operaciones
            .Where(x => x.Tipo == TipoOperacionRuta.Entrega)
            .Sum(x => x.VolumenM3);

        ValidarCapacidad(ruta, pesoActual, volumenActual);

        // Conservamos el criterio actual de prioridad por franja.
        var ordenadas = operaciones
            .OrderBy(x => x.Hasta)
            .ThenBy(x => x.Desde)
            .ThenBy(x => x.Localidad)
            .ThenBy(x => x.Id)
            .ToList();

        // Reutilizamos el planificador de horarios existente.
        var paradas = ordenadas
            .Select(x => new ParadaPlanificada(
                x.Id,
                x.Desde,
                x.Hasta,
                x.MinutosTraslado))
            .ToList();

        var horarios = PlanificadorHorarios.ValidarYCalcular(
            ruta.HoraSalida.Value,
            paradas);

        var resultado = new List<HorarioOperacionRuta>();

        for (int i = 0; i < ordenadas.Count; i++)
        {
            var operacion = ordenadas[i];
            var horario = horarios[i];

            if (operacion.Tipo == TipoOperacionRuta.Entrega)
            {
                pesoActual -= operacion.PesoKg;
                volumenActual -= operacion.VolumenM3;
            }
            else
            {
                pesoActual += operacion.PesoKg;
                volumenActual += operacion.VolumenM3;
            }

            ValidarCapacidad(ruta, pesoActual, volumenActual);

            resultado.Add(new HorarioOperacionRuta(
                operacion.Id,
                operacion.Tipo,
                horario.HoraLlegada,
                horario.HoraEntregaEstimada,
                pesoActual,
                volumenActual));
        }

        return resultado;
    }

    private static void ValidarZona(
        HojaDeRuta ruta,
        ZonaCobertura zona,
        string localidad)
    {
        if (zona.OperadorId != ruta.OperadorId ||
            !zona.Activa ||
            !zona.CubreDireccion(localidad, null))
            throw new InvalidOperationException(
                "La zona no cubre la operación.");

        if (zona.MinutosTrasladoEstimado is null or <= 0)
            throw new InvalidOperationException(
                "La zona no tiene un tiempo de traslado válido.");
    }

    private static void ValidarFranja(
        ZonaCobertura zona,
        TimeOnly desde,
        TimeOnly hasta)
    {
        if (desde >= hasta ||
            !zona.OfreceFranja(desde, hasta))
            throw new InvalidOperationException(
                "La zona no ofrece la franja solicitada.");
    }

    private static void ValidarCapacidad(
        HojaDeRuta ruta,
        decimal peso,
        decimal volumen)
    {
        if (peso < 0 || peso > ruta.CapacidadPesoKg)
            throw new InvalidOperationException(
                "Se supera la capacidad de peso.");

        if (volumen < 0 || volumen > ruta.CapacidadVolumenM3)
            throw new InvalidOperationException(
                "Se supera la capacidad de volumen.");
    }
}
