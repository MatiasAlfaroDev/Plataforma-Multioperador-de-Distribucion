using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Envios.Commands.DeclararExtravio;

public sealed class DeclararExtravioHandler(
    IEnvioRepository envios,
    IHojaDeRutaRepository hojas,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(
        DeclararExtravioCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.EnvioId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador y el envío son obligatorios.");
        }

        if (string.IsNullOrWhiteSpace(command.Justificacion))
            throw new ArgumentException(
                "Debe justificarse la declaración de extravío.");

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException(
                "Debe identificarse al responsable.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (envio.OperadorId != command.OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            command.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        // Consultamos la parada antes de modificar el estado del envío.
        var hoja = await hojas.ObtenerPorEnvioActivoAsync(
            command.OperadorId,
            command.EnvioId,
            ct);

        var estadoAnterior = envio.Estado;

        // Un envío asignado a ruta o en tránsito
        // debe tener una parada activa.
        if ((estadoAnterior == EstadoEnvio.AsignadoARuta ||
             estadoAnterior == EstadoEnvio.EnTransito) &&
            hoja is null)
        {
            throw new InvalidOperationException(
                "El envío no tiene una parada activa.");
        }

        // Evitamos dejar paradas activas asociadas
        // a estados que no deberían tenerlas.
        if (hoja is not null &&
            estadoAnterior != EstadoEnvio.AsignadoARuta &&
            estadoAnterior != EstadoEnvio.EnTransito)
        {
            throw new InvalidOperationException(
                "El estado del envío no coincide con su asignación de ruta.");
        }

        var ahora = reloj.GetUtcNow();

        await unitOfWork.EjecutarEnTransaccionAsync(
            async transaccionCt =>
            {
                if (hoja is not null)
                {
                    hoja.LiberarParadaPorExtravio(envio, ahora);
                }

                envio.Aplicar(
                    DisparadorEnvio.DeclararExtraviado,
                    reglas,
                    ahora,
                    "Backoffice",
                    command.Responsable.Trim(),
                    justificacion: command.Justificacion.Trim());

                // Una hoja despachada conserva su orden original.
                // Solo renumeramos si sigue en preparación.
                var renumerar = hoja is not null &&
                    hoja.Estado == EstadoHojaDeRuta.EnPreparacion;

                if (renumerar)
                    hoja!.PrepararRenumeracionParadas();

                await unitOfWork.GuardarCambiosAsync(transaccionCt);

                if (renumerar)
                {
                    hoja!.RenumerarParadasActivas();
                    await unitOfWork.GuardarCambiosAsync(transaccionCt);
                }
            },
            ct);
    }
}
