using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Rutas.Commands.AgregarRetiroARuta;

public sealed record AgregarRetiroARutaCommand(
    Guid OperadorId,
    Guid HojaDeRutaId,
    Guid RetiroId);

public sealed class AgregarRetiroARutaHandler(
    IHojaDeRutaRepository hojas,
    IRetiroRepository retiros,
    IEnvioRepository envios,
    IZonaCoberturaRepository zonas,
    IUnitOfWork unitOfWork,
    RegistrarAvisosRetiroHandler avisos,
    TimeProvider reloj)
{
    public async Task<IReadOnlyList<HorarioOperacionRuta>> Handle(
        AgregarRetiroARutaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.HojaDeRutaId == Guid.Empty ||
            command.RetiroId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador, la hoja y el retiro son obligatorios.");
        }

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

        var retiro = await retiros.ObtenerAsync(
            command.RetiroId, ct)
            ?? throw new NoEncontradoException(
                "El retiro no existe.");

        if (retiro.OperadorId != command.OperadorId)
            throw new NoEncontradoException(
                "El retiro no existe para este operador.");

        if (retiro.Estado != EstadoRetiro.Solicitado ||
            retiro.HojaDeRutaId.HasValue)
        {
            throw new InvalidOperationException(
                "El retiro no está disponible para asignación.");
        }

        if (retiro.FechaSolicitada != hoja.Fecha)
            throw new InvalidOperationException(
                "El retiro corresponde a otra fecha.");

        if (hoja.Paradas.Any(p =>
            p.Activa && p.RetiroId == retiro.Id))
        {
            throw new InvalidOperationException(
                "El retiro ya pertenece a la hoja.");
        }

        var zonasOperador = await zonas.ListarPorOperadorAsync(
            command.OperadorId, ct);

        var seleccionEnvios = new List<EnvioParaPlanificar>();
        var seleccionRetiros = new List<RetiroParaPlanificar>();

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

        // Recuperar todas las paradas existentes.
        foreach (var parada in hoja.Paradas.Where(p => p.Activa))
        {
            if (parada.EnvioId is Guid envioId)
            {
                var envio = await envios.ObtenerAsync(envioId, ct)
                    ?? throw new NoEncontradoException(
                        $"No existe el envío {envioId}.");

                if (envio.OperadorId != command.OperadorId ||
                    envio.Estado != EstadoEnvio.AsignadoARuta)
                {
                    throw new InvalidOperationException(
                        "Una entrega activa tiene un estado inválido.");
                }

                seleccionEnvios.Add(new EnvioParaPlanificar(
                    envio,
                    ObtenerZona(envio.Direccion.Localidad)));
            }
            else if (parada.RetiroId is Guid retiroExistenteId)
            {
                var existente = await retiros.ObtenerAsync(
                    retiroExistenteId, ct)
                    ?? throw new NoEncontradoException(
                        $"No existe el retiro {retiroExistenteId}.");

                if (existente.OperadorId != command.OperadorId ||
                    existente.Estado != EstadoRetiro.AsignadoARuta ||
                    existente.HojaDeRutaId != hoja.Id)
                {
                    throw new InvalidOperationException(
                        "Un retiro activo tiene un estado inválido.");
                }

                seleccionRetiros.Add(new RetiroParaPlanificar(
                    existente,
                    ObtenerZona(existente.DomicilioRetiro.Localidad)));
            }
            else
            {
                throw new InvalidOperationException(
                    "La hoja contiene una parada inválida.");
            }
        }

        // Incorporar el retiro solicitado a la planificación.
        seleccionRetiros.Add(new RetiroParaPlanificar(
            retiro,
            ObtenerZona(retiro.DomicilioRetiro.Localidad)));

        // Validar toda la ruta antes de modificar entidades.
        var horarios = PlanificacionHojaDeRuta.Calcular(
            hoja,
            seleccionEnvios,
            seleccionRetiros);

        await unitOfWork.EjecutarEnTransaccionAsync(
        async transaccionCt =>
        {
            hoja.AgregarRetiro(retiro);

            retiro.AsignarARuta(hoja.Id);

            // Primera fase: órdenes temporales.
            hoja.PrepararRenumeracionParadas();

            await unitOfWork.GuardarCambiosAsync(
                transaccionCt);

            // Segunda fase: orden definitivo.
            hoja.ActualizarOrdenOperaciones(horarios);

            // RF41: aviso de asignación a ruta.
            await avisos.PrepararAsync(
                retiro,
                TiposAviso.RetiroAsignadoARuta,
                $"retiro:{retiro.Id}:asignado:{hoja.Id}",
                reloj.GetUtcNow(),
                transaccionCt);

            await unitOfWork.GuardarCambiosAsync(
                transaccionCt);
        },
        ct);

        return horarios;
    }
}