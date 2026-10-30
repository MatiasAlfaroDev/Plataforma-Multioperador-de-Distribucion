using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.AgregarEnvioARuta;

public sealed class AgregarEnvioARutaHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IZonaCoberturaRepository zonas,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<IReadOnlyList<HorarioParada>> Handle(
        AgregarEnvioARutaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.HojaDeRutaId == Guid.Empty ||
            command.EnvioId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador, la hoja y el envío son obligatorios.");
        }

        if (command.HoraEntregaDesde >= command.HoraEntregaHasta)
            throw new ArgumentException(
                "La franja de entrega no es válida.");

        var hoja = await hojas.ObtenerAsync(
            command.OperadorId,
            command.HojaDeRutaId,
            ct)
            ?? throw new NoEncontradoException(
                "La hoja de ruta no existe.");

        hoja.VerificarEditable();

        if (!hoja.HoraSalida.HasValue)
            throw new InvalidOperationException(
                "La hoja no tiene hora de salida.");

        if (hoja.Paradas.Any(p =>
                p.EnvioId == command.EnvioId && p.Activa))
            throw new InvalidOperationException(
                "El envío ya pertenece a esta hoja.");

        var nuevo = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (nuevo.OperadorId != command.OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (nuevo.Estado != EstadoEnvio.EnDeposito &&
            nuevo.Estado != EstadoEnvio.Reprogramado)
            throw new InvalidOperationException(
                "El envío no está disponible para asignación.");

        if (nuevo.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            command.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        var zonasOperador = await zonas.ListarPorOperadorAsync(
            command.OperadorId,
            ct);

        var seleccion = new List<EnvioParaPlanificar>();

        EnvioParaPlanificar PrepararZona(Envio envio)
        {
            var compatibles = zonasOperador
                .Where(z => z.Activa &&
                    z.CubreDireccion(envio.Direccion.Localidad, null))
                .ToList();

            if (compatibles.Count != 1)
                throw new InvalidOperationException(
                    $"El envío {envio.ReferenciaExterna} " +
                    "no tiene una zona de cobertura única.");

            return new EnvioParaPlanificar(envio, compatibles[0]);
        }

        // Recuperamos los envíos ya asignados a esta hoja.
        foreach (var parada in hoja.Paradas.Where(p => p.Activa))
        {
            var existente = await envios.ObtenerAsync(parada.EnvioId, ct)
                ?? throw new NoEncontradoException(
                    $"No existe el envío {parada.EnvioId}.");

            if (existente.OperadorId != command.OperadorId ||
                existente.Estado != EstadoEnvio.AsignadoARuta)
            {
                throw new InvalidOperationException(
                    "Una parada activa contiene un envío inválido.");
            }

            seleccion.Add(PrepararZona(existente));
        }

        var nuevoConZona = PrepararZona(nuevo);

        if (!nuevoConZona.Zona.OfreceFranja(
                command.HoraEntregaDesde,
                command.HoraEntregaHasta))
        {
            throw new InvalidOperationException(
                "La zona no ofrece la franja seleccionada.");
        }

        // Se conserva un compromiso existente si coincide.
        if (nuevo.Estado == EstadoEnvio.EnDeposito)
        {
            var tieneCompromiso =
                nuevo.FechaEntregaComprometida.HasValue ||
                nuevo.HoraEntregaDesde.HasValue ||
                nuevo.HoraEntregaHasta.HasValue;

            if (tieneCompromiso)
            {
                if (nuevo.FechaEntregaComprometida != hoja.Fecha ||
                    nuevo.HoraEntregaDesde != command.HoraEntregaDesde ||
                    nuevo.HoraEntregaHasta != command.HoraEntregaHasta)
                {
                    throw new InvalidOperationException(
                        "El envío ya tiene otro compromiso de entrega.");
                }
            }
            else
            {
                nuevo.EstablecerCompromisoEntrega(
                    hoja.Fecha,
                    command.HoraEntregaDesde,
                    command.HoraEntregaHasta);
            }
        }
        else
        {
            var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById(
                "America/Montevideo");

            var fechaLocal = DateTime.SpecifyKind(
                hoja.Fecha.ToDateTime(command.HoraEntregaDesde),
                DateTimeKind.Unspecified);

            var inicioNuevaEntrega = new DateTimeOffset(
                fechaLocal,
                zonaHoraria.GetUtcOffset(fechaLocal));

            if (inicioNuevaEntrega <= reloj.GetUtcNow())
                throw new InvalidOperationException(
                    "La nueva entrega debe programarse para una fecha futura.");

            nuevo.EstablecerCompromisoEntrega(
                hoja.Fecha,
                command.HoraEntregaDesde,
                command.HoraEntregaHasta,
                reglas,
                inicioNuevaEntrega);
        }

        seleccion.Add(nuevoConZona);

        // Valida TODA la hoja antes de agregar la nueva parada.
        var horarios = PlanificacionHojaDeRuta.Calcular(
            hoja,
            seleccion);

        var ahora = reloj.GetUtcNow();

        await unitOfWork.EjecutarEnTransaccionAsync(
            async transaccionCt =>
            {
                // Agrega el envío y reserva su parada.
                hoja.AgregarEnvio(nuevo);

                // Mueve temporalmente las paradas activas
                // para evitar colisiones con el índice único.
                hoja.PrepararRenumeracionParadas();

                nuevo.Aplicar(
                    DisparadorEnvio.AsignarARuta,
                    reglas,
                    ahora,
                    "Backoffice",
                    "Despachador");

                // Primera fase: persiste las paradas con órdenes temporales.
                await unitOfWork.GuardarCambiosAsync(transaccionCt);

                // Segunda fase: establece el orden calculado para la ruta.
                hoja.ActualizarOrdenParadas(horarios);

                await unitOfWork.GuardarCambiosAsync(transaccionCt);
            },
            ct);

        return horarios;
    }
}