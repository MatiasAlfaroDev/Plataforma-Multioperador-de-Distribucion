using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Queries.ConsultarDisponibilidadReprogramacionPublica;

public sealed class ConsultarDisponibilidadReprogramacionPublicaHandler(
    IEnvioRepository envios,
    ISolicitudReprogramacionRepository solicitudes,
    IVersionReglasOperativasRepository reglasRepository)
{
    public async Task<DisponibilidadReprogramacionPublicaDto> Handle(
        Guid envioId,
        CancellationToken ct)
    {
        var envio = await envios.ObtenerAsync(envioId, ct);

        if (envio is null)
            return new(false, "El envío no existe.");

        if (envio.Estado != EstadoEnvio.EnDeposito)
            return new(false, "El envío no está en depósito.");

        if (envio.IntentosRealizados == 0)
            return new(false, "El envío todavía no tiene intentos fallidos.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            return new(false, "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            envio.OperadorId,
            versionId,
            ct);

        if (reglas is null)
            return new(false, "No se encontraron las reglas operativas.");

        if (envio.IntentosRealizados >= reglas.MaximoIntentosEntrega ||
            envio.UltimoMotivoRequiereDevolucion)
        {
            return new(false, "El envío no admite nuevos intentos de entrega.");
        }

        if (await solicitudes.ExistePendienteAsync(envio.Id, ct))
            return new(false, "Ya existe una solicitud pendiente.");

        return new(true, null);
    }
}