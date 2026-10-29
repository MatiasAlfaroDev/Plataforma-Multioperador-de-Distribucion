
using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Envios.Eventos;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;

public sealed class RegistrarEventoMovilHandler(
    IEnvioRepository envios,
    IHojaDeRutaRepository hojasDeRuta,
    IEventoMovilRepository eventosMovil,
    IVersionReglasOperativasRepository versionesReglas,
    IMotivoNoEntregaRepository motivos,
    IUnitOfWork unitOfWork,
    IPublicadorDeEventos eventos,
    IValidator<RegistrarEventoMovilCommand> validator,
    TimeProvider reloj)
{
    public async Task Handle(
        RegistrarEventoMovilCommand command,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        // Idempotencia de la sincronización.
        if (await eventosMovil.ExisteAsync(command.IdEvento, ct))
            throw new ConflictoException("El evento ya fue registrado.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        var entregaPrevia =
            await eventosMovil.ObtenerEntregaAsync(envio.Id, ct);

        if (entregaPrevia is not null)
            throw new InvalidOperationException(
                $"Conflicto: el envío ya figura como entregado a " +
                $"{entregaPrevia.Receptor} " +
                $"({entregaPrevia.OcurridoEn:dd/MM/yyyy HH:mm} UTC).");

        if (envio.Estado != EstadoEnvio.EnTransito)
            throw new InvalidOperationException(
                "El envío debe estar en tránsito para registrar una entrega o intento fallido.");
     
        if (envio.VersionReglasOperativasId is not Guid versionReglasId)
            throw new InvalidOperationException(
                "El envío no tiene una versión de reglas operativas asignada.");

        var reglas = await versionesReglas.ObtenerPorIdAsync(
            envio.OperadorId,
            versionReglasId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontró la versión de reglas operativas del envío.");

        // Validamos el evento antes de modificar el estado.
        var tipo = Enum.Parse<TipoEventoMovil>(
            command.Tipo,
            ignoreCase: true);

        var evento = EventoMovil.Registrar(
            command.IdEvento,
            envio,
            tipo,
            command.Motivo,
            command.Receptor,
            command.FechaHora,
            reloj.GetUtcNow());

        var ocurridoEn = evento.OcurridoEn;

        if (tipo == TipoEventoMovil.Entregado)
        {
            if (!command.Latitud.HasValue ||
                !command.Longitud.HasValue)
                throw new ArgumentException(
                    "La entrega debe incluir las coordenadas del dispositivo.");

            var prueba = PruebaEntrega.Crear(
                command.Receptor!,
                command.DocumentoReceptor,
                command.ReferenciaFirma,
                command.ReferenciaFotografia,
                ocurridoEn,
                command.Latitud.Value,
                command.Longitud.Value);

            envio.Aplicar(
                DisparadorEnvio.Entregar,
                reglas,
                ocurridoEn,
                "Mobile",
                "Mobile",
                pruebaEntrega: prueba,
                latitud: command.Latitud,
                longitud: command.Longitud);
        }
        else if (tipo == TipoEventoMovil.NoEntregado)
        {
            if (string.IsNullOrWhiteSpace(command.EvidenciaFallo))
                throw new ArgumentException(
                    "El intento fallido debe incluir evidencia.");

            var motivo = await motivos.ObtenerPorDescripcionAsync(
                envio.OperadorId,
                versionReglasId,
                command.Motivo!,
                ct)
                ?? throw new NoEncontradoException(
                    "El motivo de no entrega no existe en el catálogo del operador.");

            envio.Aplicar(
                DisparadorEnvio.RegistrarIntentoFallido,
                reglas,
                ocurridoEn,
                "Mobile",
                "Mobile",
                motivoNoEntrega: motivo,
                evidenciaFallo: command.EvidenciaFallo,
                latitud: command.Latitud,
                longitud: command.Longitud);
        }
        else
        {
            throw new ArgumentException(
                "El tipo de evento móvil no es válido.");
        }

        var hoja = await hojasDeRuta.ObtenerPorEnvioActivoAsync(
            envio.OperadorId,
            envio.Id,
            ct)
            ?? throw new InvalidOperationException(
                "El envío no tiene una parada activa en ninguna hoja de ruta.");

        hoja.FinalizarParada(envio, ocurridoEn);

        await eventosMovil.AgregarAsync(evento, ct);

        await eventos.PublicarAsync(
            new EventoMovilRegistrado(
                evento.Id,
                evento.EnvioId,
                evento.OperadorId,
                evento.Tipo.ToString(),
                evento.OcurridoEn),
            ct);

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
