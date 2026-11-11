
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.CrearHojaDeRuta;

public sealed class CrearHojaDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IRepartidorRepository repartidores,
    IVehiculoRepository vehiculos,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    IZonaCoberturaRepository zonas,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<Guid> Handle(
        CrearHojaDeRutaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.RepartidorId == Guid.Empty ||
            command.VehiculoId == Guid.Empty)
            throw new ArgumentException(
                "Operador, repartidor y vehículo son obligatorios.");

        var enviosIds = command.EnviosIds ?? [];
        var retirosIds = command.RetirosIds ?? [];

        if (enviosIds.Count + retirosIds.Count == 0)
            throw new ArgumentException(
                "Seleccioná al menos una entrega o un retiro.");

        if (enviosIds.Any(id => id == Guid.Empty) ||
            enviosIds.Distinct().Count() != enviosIds.Count)
            throw new ArgumentException(
                "La selección contiene envíos inválidos o repetidos.");

        if (retirosIds.Any(id => id == Guid.Empty) ||
            retirosIds.Distinct().Count() != retirosIds.Count)
            throw new ArgumentException(
                "La selección contiene retiros inválidos o repetidos.");

        if (enviosIds.Count > 0 &&
            command.HoraEntregaDesde >= command.HoraEntregaHasta)
            throw new ArgumentException(
                "La franja de entrega seleccionada no es válida.");

        var repartidor = await repartidores.ObtenerAsync(
            command.OperadorId,
            command.RepartidorId,
            ct)
            ?? throw new InvalidOperationException(
                "El repartidor no existe o está inactivo.");

        var vehiculo = await vehiculos.ObtenerAsync(
            command.OperadorId,
            command.VehiculoId,
            ct)
            ?? throw new InvalidOperationException(
                "El vehículo no existe o está inactivo.");

        var hoja = HojaDeRuta.Crear(
            command.OperadorId,
            repartidor,
            vehiculo,
            command.Fecha,
            command.MaximoParadas);

        hoja.ConfigurarHoraSalida(command.HoraSalida);

        var zonasOperador = await zonas.ListarPorOperadorAsync(
            command.OperadorId,
            ct);

        ZonaCobertura ObtenerZona(string localidad)
        {
            var compatibles = zonasOperador
                .Where(z =>
                    z.Activa &&
                    z.CubreDireccion(localidad, null))
                .ToList();

            if (compatibles.Count != 1)
                throw new InvalidOperationException(
                    $"La localidad {localidad} no tiene una zona de cobertura única.");

            return compatibles[0];
        }

        var seleccionEnvios = new List<EnvioParaPlanificar>();
        var seleccionRetiros = new List<RetiroParaPlanificar>();

        var reglasPorEnvio =
            new Dictionary<Guid, VersionReglasOperativas>();

        foreach (var envioId in enviosIds)
        {
            var envio = await envios.ObtenerAsync(envioId, ct)
                ?? throw new InvalidOperationException(
                    $"No existe el envío {envioId}.");

            if (envio.OperadorId != command.OperadorId)
                throw new InvalidOperationException(
                    "El envío pertenece a otro operador.");

            if (envio.Estado != EstadoEnvio.EnDeposito &&
                envio.Estado != EstadoEnvio.Reprogramado)
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} no está disponible para asignación.");

            if (envio.VersionReglasOperativasId is not Guid reglasId)
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} no tiene reglas operativas asignadas.");

            var reglas = await reglasRepository.ObtenerPorIdAsync(
                command.OperadorId,
                reglasId,
                ct)
                ?? throw new InvalidOperationException(
                    "No se encontraron las reglas operativas del envío.");

            var zona = ObtenerZona(envio.Direccion.Localidad);

            if (!zona.OfreceFranja(
                    command.HoraEntregaDesde,
                    command.HoraEntregaHasta))
                throw new InvalidOperationException(
                    $"La zona del envío {envio.ReferenciaExterna} no ofrece la franja seleccionada.");

            if (envio.Estado == EstadoEnvio.EnDeposito)
            {
                var tieneCompromiso =
                    envio.FechaEntregaComprometida.HasValue ||
                    envio.HoraEntregaDesde.HasValue ||
                    envio.HoraEntregaHasta.HasValue;

                if (tieneCompromiso)
                {
                    if (envio.FechaEntregaComprometida != command.Fecha ||
                        envio.HoraEntregaDesde != command.HoraEntregaDesde ||
                        envio.HoraEntregaHasta != command.HoraEntregaHasta)
                        throw new InvalidOperationException(
                            $"El envío {envio.ReferenciaExterna} ya tiene otro compromiso de entrega.");
                }
                else
                {
                    envio.EstablecerCompromisoEntrega(
                        command.Fecha,
                        command.HoraEntregaDesde,
                        command.HoraEntregaHasta);
                }
            }
            else
            {
                var zonaHoraria =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "America/Montevideo");

                var fechaHoraLocal = DateTime.SpecifyKind(
                    command.Fecha.ToDateTime(
                        command.HoraEntregaDesde),
                    DateTimeKind.Unspecified);

                var inicioNuevaEntrega = new DateTimeOffset(
                    fechaHoraLocal,
                    zonaHoraria.GetUtcOffset(fechaHoraLocal));

                if (inicioNuevaEntrega <= reloj.GetUtcNow())
                    throw new InvalidOperationException(
                        "La nueva entrega debe programarse para una fecha futura.");

                envio.EstablecerCompromisoEntrega(
                    command.Fecha,
                    command.HoraEntregaDesde,
                    command.HoraEntregaHasta,
                    reglas,
                    inicioNuevaEntrega);
            }

            seleccionEnvios.Add(
                new EnvioParaPlanificar(envio, zona));

            reglasPorEnvio.Add(envio.Id, reglas);
        }

        foreach (var retiroId in retirosIds)
        {
            var retiro = await retiros.ObtenerAsync(
                retiroId, ct)
                ?? throw new InvalidOperationException(
                    $"No existe el retiro {retiroId}.");

            if (retiro.OperadorId != command.OperadorId)
                throw new InvalidOperationException(
                    "El retiro pertenece a otro operador.");

            if (retiro.Estado != EstadoRetiro.Solicitado ||
                retiro.HojaDeRutaId.HasValue)
                throw new InvalidOperationException(
                    "El retiro no está disponible para asignación.");

            if (retiro.FechaSolicitada != command.Fecha)
                throw new InvalidOperationException(
                    "La fecha solicitada del retiro no coincide con la hoja.");

            var zona = ObtenerZona(
                retiro.DomicilioRetiro.Localidad);

            seleccionRetiros.Add(
                new RetiroParaPlanificar(retiro, zona));
        }

        // Planificación única para entregas, retiros o ambos.
        var horarios = PlanificacionHojaDeRuta.Calcular(
            hoja,
            seleccionEnvios,
            seleccionRetiros);

        var enviosPorId = seleccionEnvios.ToDictionary(
            x => x.Envio.Id,
            x => x.Envio);

        var retirosPorId = seleccionRetiros.ToDictionary(
            x => x.Retiro.Id,
            x => x.Retiro);

        var ahora = reloj.GetUtcNow();

        foreach (var horario in horarios)
        {
            if (horario.Tipo == TipoOperacionRuta.Entrega)
            {
                var envio = enviosPorId[horario.ReferenciaId];

                hoja.AgregarEnvio(envio);

                envio.Aplicar(
                    DisparadorEnvio.AsignarARuta,
                    reglasPorEnvio[envio.Id],
                    ahora,
                    "Backoffice",
                    "Despachador");
            }
            else
            {
                var retiro = retirosPorId[horario.ReferenciaId];

                hoja.AgregarRetiro(retiro);
                retiro.AsignarARuta(hoja.Id);
            }
        }

        await hojas.AgregarAsync(hoja, ct);
        await unitOfWork.GuardarCambiosAsync(ct);

        return hoja.Id;
    }
}
