
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.AgregarEnvioARuta;

public sealed class AgregarEnvioARutaHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IRetiroRepository retiros,
    IZonaCoberturaRepository zonas,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<IReadOnlyList<HorarioOperacionRuta>> Handle(
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

        if (hoja.Paradas.Any(p =>
            p.Activa &&
            p.TipoOperacion is (
                TipoOperacionRuta.DevolucionComercio or
                TipoOperacionRuta.EntregaRetiroComercio)))
        {
            throw new InvalidOperationException(
                "No se pueden agregar operaciones a una hoja que contiene entregas al comercio.");
        }

        if (!hoja.HoraSalida.HasValue)
            throw new InvalidOperationException(
                "La hoja no tiene hora de salida.");

        if (hoja.Paradas.Any(p =>
            p.EnvioId == command.EnvioId && p.Activa))
        {
            throw new InvalidOperationException(
                "El envío ya pertenece a esta hoja.");
        }

        var nuevo = await envios.ObtenerAsync(
            command.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (nuevo.OperadorId != command.OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (nuevo.Estado != EstadoEnvio.EnDeposito &&
            nuevo.Estado != EstadoEnvio.Reprogramado)
        {
            throw new InvalidOperationException(
                "El envío no está disponible para asignación.");
        }

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
            command.OperadorId, ct);

        UltimaMilla.Domain.Configuracion.ZonaCobertura
            ObtenerZona(string localidad)
        {
            var compatibles = zonasOperador
                .Where(z =>
                    z.Activa &&
                    z.CubreDireccion(localidad, null))
                .ToList();

            if (compatibles.Count != 1)
                throw new InvalidOperationException(
                    "La dirección no tiene una zona de cobertura única.");

            return compatibles[0];
        }

        var seleccionEnvios = new List<EnvioParaPlanificar>();
        var seleccionRetiros = new List<RetiroParaPlanificar>();

        // Recuperar todas las operaciones activas de la ruta.
        foreach (var parada in hoja.Paradas.Where(p => p.Activa))
        {
            if (parada.EnvioId is Guid envioId)
            {
                var existente = await envios.ObtenerAsync(envioId, ct)
                    ?? throw new NoEncontradoException(
                        $"No existe el envío {envioId}.");

                if (existente.OperadorId != command.OperadorId ||
                    existente.Estado != EstadoEnvio.AsignadoARuta)
                {
                    throw new InvalidOperationException(
                        "Una entrega activa tiene un estado inválido.");
                }

                seleccionEnvios.Add(new EnvioParaPlanificar(
                    existente,
                    ObtenerZona(existente.Direccion.Localidad)));
            }
            else if (parada.RetiroId is Guid retiroId)
            {
                var retiro = await retiros.ObtenerAsync(retiroId, ct)
                    ?? throw new NoEncontradoException(
                        $"No existe el retiro {retiroId}.");

                if (retiro.OperadorId != command.OperadorId ||
                    retiro.Estado != EstadoRetiro.AsignadoARuta ||
                    retiro.HojaDeRutaId != hoja.Id)
                {
                    throw new InvalidOperationException(
                        "Un retiro activo tiene un estado inválido.");
                }

                seleccionRetiros.Add(new RetiroParaPlanificar(
                    retiro,
                    ObtenerZona(retiro.DomicilioRetiro.Localidad)));
            }
            else
            {
                throw new InvalidOperationException(
                    "La hoja contiene una parada inválida.");
            }
        }

        var zonaNueva = ObtenerZona(nuevo.Direccion.Localidad);

        if (!zonaNueva.OfreceFranja(
            command.HoraEntregaDesde,
            command.HoraEntregaHasta))
        {
            throw new InvalidOperationException(
                "La zona no ofrece la franja seleccionada.");
        }

        // Mantener las reglas de compromiso de entrega existentes.
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

        seleccionEnvios.Add(new EnvioParaPlanificar(
            nuevo,
            zonaNueva));

        // El cálculo considera todas las entregas y retiros.
        var horarios = PlanificacionHojaDeRuta.Calcular(
            hoja,
            seleccionEnvios,
            seleccionRetiros);

        var ahora = reloj.GetUtcNow();

        await unitOfWork.EjecutarEnTransaccionAsync(
            async transaccionCt =>
            {
                hoja.AgregarEnvio(nuevo);

                hoja.PrepararRenumeracionParadas();

                nuevo.Aplicar(
                    DisparadorEnvio.AsignarARuta,
                    reglas,
                    ahora,
                    "Backoffice",
                    "Despachador");

                await unitOfWork.GuardarCambiosAsync(
                    transaccionCt);

                hoja.ActualizarOrdenOperaciones(horarios);

                await unitOfWork.GuardarCambiosAsync(
                    transaccionCt);
            },
            ct);

        return horarios;
    }
}
