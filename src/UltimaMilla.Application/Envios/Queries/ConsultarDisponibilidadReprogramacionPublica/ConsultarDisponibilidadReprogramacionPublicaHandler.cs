using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.Commands.Reprogramacion;
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

        if (!SolicitarReprogramacionHandler.EstadosQueAdmitenSolicitud.Contains(envio.Estado) ||
            envio.IntentosRealizados == 0)
        {
            return new(false, "Vas a poder pedir una nueva fecha si un intento de entrega no se puede completar.");
        }

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
            return new(false, "Este envío ya no admite nuevos intentos de entrega.");
        }

        if (await solicitudes.ExistePendienteAsync(envio.Id, ct))
            return new(false, "Ya pediste una reprogramación; el operador la está revisando.");

        return new(true, null);
    }
}