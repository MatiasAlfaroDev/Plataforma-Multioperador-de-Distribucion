using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Envios.Commands.DeclararExtravioBulto;

/// <summary>
/// Extravío de un solo bulto. Si era el último a cargo del operador, el envío pasa a Extraviado.
/// </summary>
public sealed class DeclararExtravioBultoHandler(
    IEnvioRepository envios,
    IHojaDeRutaRepository hojas,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(
        DeclararExtravioBultoCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.EnvioId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador y el envío son obligatorios.");
        }

        if (string.IsNullOrWhiteSpace(command.Identificacion))
            throw new ArgumentException(
                "Debe indicarse el bulto.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (envio.OperadorId != command.OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        var hoja = await hojas.ObtenerPorEnvioActivoAsync(
            command.OperadorId,
            command.EnvioId,
            ct);

        var enRuta = envio.Estado is EstadoEnvio.AsignadoARuta or EstadoEnvio.EnTransito;

        if (enRuta && hoja is null)
            throw new InvalidOperationException(
                "El envío no tiene una parada activa.");

        if (!enRuta && hoja is not null)
            throw new InvalidOperationException(
                "El estado del envío no coincide con su asignación de ruta.");

        var ahora = reloj.GetUtcNow();
        var responsable = command.Responsable?.Trim() ?? string.Empty;

        envio.DeclararBultoExtraviado(
            command.Identificacion,
            command.Justificacion,
            responsable,
            ahora);

        // Si todavía quedan bultos a cargo del operador, el envío sigue su curso (y su parada, si la tiene).
        if (envio.BultosPendientes > 0 || envio.BultosEnDevolucion > 0)
        {
            await unitOfWork.GuardarCambiosAsync(ct);
            return;
        }

        // Era el último: el envío completo se da por extraviado y libera su parada.
        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            command.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        await unitOfWork.EjecutarEnTransaccionAsync(
            async transaccionCt =>
            {
                hoja?.LiberarParadaPorExtravio(envio, ahora);

                envio.Aplicar(
                    DisparadorEnvio.DeclararExtraviado,
                    reglas,
                    ahora,
                    "Backoffice",
                    responsable,
                    justificacion: command.Justificacion.Trim());

                // Una hoja despachada conserva su orden original.
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
