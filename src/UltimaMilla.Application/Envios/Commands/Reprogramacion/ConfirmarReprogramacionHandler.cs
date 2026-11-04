using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.Reprogramacion;

public sealed class ConfirmarReprogramacionHandler(
    ISolicitudReprogramacionRepository solicitudes,
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(
        ConfirmarReprogramacionCommand command,
        CancellationToken ct)
    {
        if (command.SolicitudId == Guid.Empty)
            throw new ArgumentException("La solicitud es obligatoria.");

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException("El responsable es obligatorio.");

        var ahora = reloj.GetUtcNow();

        var solicitud = await solicitudes.ObtenerAsync(
            command.SolicitudId, ct)
            ?? throw new NoEncontradoException(
                "La solicitud de reprogramación no existe.");

        if (solicitud.Estado != EstadoSolicitudReprogramacion.Pendiente)
            throw new InvalidOperationException(
                "La solicitud ya fue procesada.");

        var envio = await envios.ObtenerAsync(solicitud.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (envio.OperadorId != solicitud.OperadorId)
            throw new InvalidOperationException(
                "La solicitud no corresponde al operador del envío.");

        if (envio.Estado != EstadoEnvio.EnDeposito)
            throw new InvalidOperationException(
                "El envío debe estar en depósito para confirmar la reprogramación.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            envio.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        envio.Aplicar(
            DisparadorEnvio.Reprogramar,
            reglas,
            ahora,
            "Backoffice",
            command.Responsable.Trim());

        solicitud.MarcarAtendida(ahora);

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}