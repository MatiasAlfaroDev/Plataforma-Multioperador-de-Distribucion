using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion.Commands.ActualizarTarifa;

public sealed class ActualizarTarifaHandler(
    IVersionTarifariaRepository tarifas,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<Guid> Handle(
        ActualizarTarifaCommand command,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();

        var versionActual = await tarifas.ObtenerVigentePorZonaAsync(
            command.OperadorId,
            command.ZonaCoberturaId,
            ahora,
            ct);

        // Si existe una versión anterior, cerramos su vigencia.
        if (versionActual is not null)
        {
            versionActual.FinalizarVigencia(ahora);
        }

        // Creamos una nueva versión con los nuevos valores.
        var nuevaVersion = VersionTarifaria.Crear(
            command.OperadorId,
            command.CuadroTarifarioId,
            command.ZonaCoberturaId,
            command.PrecioBase,
            command.PrecioPorKg,
            command.PrecioPorMetroCubico,
            command.RecargoUrgentePorcentaje,
            command.BonificacionPorcentaje,
            ahora);

        await tarifas.AgregarAsync(nuevaVersion, ct);

        await unitOfWork.GuardarCambiosAsync(ct);

        return nuevaVersion.Id;
    }
}