
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.CrearHojaDeRuta;

public sealed class CrearHojaDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IRepartidorRepository repartidores,
    IVehiculoRepository vehiculos,
    IEnvioRepository envios,
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
        {
            throw new ArgumentException(
                "Operador, repartidor y vehículo son obligatorios.");
        }

        if (command.EnviosIds is null ||
            command.EnviosIds.Count == 0)
        {
            throw new ArgumentException(
                "Seleccioná al menos un envío.");
        }

        if (command.HoraEntregaDesde >= command.HoraEntregaHasta)
            throw new ArgumentException(
                "La franja de entrega seleccionada no es válida.");

        if (command.EnviosIds.Any(id => id == Guid.Empty) ||
            command.EnviosIds.Distinct().Count() !=
                command.EnviosIds.Count)
        {
            throw new ArgumentException(
                "La selección contiene envíos inválidos o repetidos.");
        }

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

        var seleccion = new List<EnvioParaPlanificar>();
        var reglasPorEnvio = new Dictionary<Guid,
            UltimaMilla.Domain.Configuracion.VersionReglasOperativas>();

        foreach (var envioId in command.EnviosIds)
        {
            var envio = await envios.ObtenerAsync(envioId, ct)
                ?? throw new InvalidOperationException(
                    $"No existe el envío {envioId}.");

            if (envio.OperadorId != command.OperadorId)
                throw new InvalidOperationException(
                    "El envío pertenece a otro operador.");

            if (envio.Estado != EstadoEnvio.EnDeposito &&
                envio.Estado != EstadoEnvio.Reprogramado)
            {
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} " +
                    "no está disponible para asignación.");
            }

            if (envio.VersionReglasOperativasId is not Guid reglasId)
            {
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} " +
                    "no tiene reglas operativas asignadas.");
            }

            var reglas = await reglasRepository.ObtenerPorIdAsync(
                command.OperadorId,
                reglasId,
                ct)
                ?? throw new InvalidOperationException(
                    "No se encontraron las reglas operativas del envío.");

            var zonasCompatibles = zonasOperador
                .Where(z =>
                    z.Activa &&
                    z.CubreDireccion(
                        envio.Direccion.Localidad,
                        null))
                .ToList();

            if (zonasCompatibles.Count != 1)
            {
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} " +
                    "no tiene una zona de cobertura única " +
                    "para su localidad.");
            }

            var zona = zonasCompatibles[0];

            if (!zona.OfreceFranja(
                    command.HoraEntregaDesde,
                    command.HoraEntregaHasta))
            {
                throw new InvalidOperationException(
                    $"La zona del envío {envio.ReferenciaExterna} " +
                    "no ofrece la franja horaria seleccionada.");
            }

            if (envio.Estado == EstadoEnvio.EnDeposito)
            {
                if (envio.FechaEntregaComprometida.HasValue ||
                    envio.HoraEntregaDesde.HasValue ||
                    envio.HoraEntregaHasta.HasValue)
                {
                    if (envio.FechaEntregaComprometida != command.Fecha ||
                        envio.HoraEntregaDesde != command.HoraEntregaDesde ||
                        envio.HoraEntregaHasta != command.HoraEntregaHasta)
                    {
                        throw new InvalidOperationException(
                            $"El envío {envio.ReferenciaExterna} ya tiene " +
                            "otro compromiso de entrega.");
                    }
                }
                else
                {
                    envio.EstablecerCompromisoEntrega(
                        command.Fecha,
                        command.HoraEntregaDesde,
                        command.HoraEntregaHasta);
                }
            }
            else if (envio.Estado == EstadoEnvio.Reprogramado)
            {
                var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById(
                    "America/Montevideo");

                var fechaHoraLocal = DateTime.SpecifyKind(
                    command.Fecha.ToDateTime(command.HoraEntregaDesde),
                    DateTimeKind.Unspecified);

                var inicioNuevaEntrega = new DateTimeOffset(
                    fechaHoraLocal,
                    zonaHoraria.GetUtcOffset(fechaHoraLocal));

                if (inicioNuevaEntrega <= reloj.GetUtcNow())
                {
                    throw new InvalidOperationException(
                        "La nueva entrega debe programarse para una fecha futura.");
                }

                envio.EstablecerCompromisoEntrega(
                    command.Fecha,
                    command.HoraEntregaDesde,
                    command.HoraEntregaHasta,
                    reglas,
                    inicioNuevaEntrega);
            }

            seleccion.Add(new EnvioParaPlanificar(
                envio,
                zona));

            reglasPorEnvio.Add(envio.Id, reglas);
        }

        // Valida fechas, franjas, capacidad y cantidad de paradas,
        // y devuelve las paradas en el orden planificado.
        var horarios = PlanificacionHojaDeRuta.Calcular(
            hoja,
            seleccion);

        var enviosPorId = seleccion.ToDictionary(
            x => x.Envio.Id,
            x => x.Envio);

        var ahora = reloj.GetUtcNow();

        foreach (var horario in horarios)
        {
            var envio = enviosPorId[horario.EnvioId];

            // Agregar en el orden calculado, no en el de la selección.
            hoja.AgregarEnvio(envio);

            envio.Aplicar(
                DisparadorEnvio.AsignarARuta,
                reglasPorEnvio[envio.Id],
                ahora,
                "Backoffice",
                "Despachador");
        }

        await hojas.AgregarAsync(hoja, ct);

        // Una sola operación de persistencia:
        // hoja + paradas + nuevos estados + eventos.
        await unitOfWork.GuardarCambiosAsync(ct);

        return hoja.Id;
    }
}
