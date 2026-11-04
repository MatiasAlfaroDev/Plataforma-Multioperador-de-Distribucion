using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.Reprogramacion;

public sealed class SolicitarReprogramacionHandler(
    IEnvioRepository envios,
    ISolicitudReprogramacionRepository solicitudes,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<Guid> Handle(
        SolicitarReprogramacionCommand command,
        CancellationToken ct)
    {
        if (command.EnvioId == Guid.Empty)
            throw new ArgumentException("El envío es obligatorio.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        if (envio.Estado != EstadoEnvio.EnDeposito)
            throw new InvalidOperationException(
                "Solo se puede solicitar la reprogramación de un envío en depósito.");

        if (envio.IntentosRealizados == 0)
            throw new InvalidOperationException(
                "El envío todavía no tiene intentos fallidos.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            envio.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        if (envio.IntentosRealizados >= reglas.MaximoIntentosEntrega ||
            envio.UltimoMotivoRequiereDevolucion)
        {
            throw new InvalidOperationException(
                "El envío no admite nuevos intentos de entrega.");
        }

        if (await solicitudes.ExistePendienteAsync(envio.Id, ct))
            throw new InvalidOperationException(
                "Ya existe una solicitud de reprogramación pendiente.");

        var solicitud = new SolicitudReprogramacion(
            envio.OperadorId,
            envio.Id,
            reloj.GetUtcNow());

        await solicitudes.AgregarAsync(solicitud, ct);
        await unitOfWork.GuardarCambiosAsync(ct);

        return solicitud.Id;
    }
}