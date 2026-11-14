using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Rutas.Commands.AgregarEnvioUrgente;

/// <param name="HoraEntregaDesde">Franja a comprometer si el envío todavía no tiene una.</param>
public sealed record AgregarEnvioUrgenteCommand(
    Guid OperadorId,
    Guid HojaDeRutaId,
    Guid EnvioId,
    TimeOnly HoraEntregaDesde,
    TimeOnly HoraEntregaHasta);

/// <summary>
/// Reasignación urgente (letra 6.11 y opcional 7.3): el despachador suma un envío del depósito a una hoja que
/// ya salió. El envío queda asignado y despachado en el mismo momento (En tránsito, a nombre del repartidor de la
/// hoja). El cambio de estado sale por el outbox y el Worker le manda el push al repartidor
/// (<c>AvisarAsignacionConsumer</c>); en la app, el bulto aparece como faltante en la carga del vehículo hasta
/// que lo levanta del depósito.
/// </summary>
public sealed class AgregarEnvioUrgenteHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<int> Handle(AgregarEnvioUrgenteCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty || command.HojaDeRutaId == Guid.Empty || command.EnvioId == Guid.Empty)
            throw new ArgumentException("El operador, la hoja y el envío son obligatorios.");

        if (command.HoraEntregaDesde >= command.HoraEntregaHasta)
            throw new ArgumentException("La franja de entrega no es válida.");

        var hoja = await hojas.ObtenerAsync(command.OperadorId, command.HojaDeRutaId, ct)
            ?? throw new NoEncontradoException("La hoja de ruta no existe.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        if (envio.OperadorId != command.OperadorId)
            throw new InvalidOperationException("El envío pertenece a otro operador.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException("El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(command.OperadorId, versionId, ct)
            ?? throw new NoEncontradoException("No se encontraron las reglas operativas del envío.");

        // Sin compromiso todavía: se compromete para el día de la hoja en la franja elegida.
        // Con compromiso (por ejemplo, reprogramado), la hoja valida que sea para su fecha.
        if (envio.Estado == EstadoEnvio.EnDeposito && envio.FechaEntregaComprometida is null)
            envio.EstablecerCompromisoEntrega(hoja.Fecha, command.HoraEntregaDesde, command.HoraEntregaHasta);

        // La hoja valida que esté despachada y el resto de las restricciones (paradas, peso, volumen).
        hoja.AgregarEnvioUrgente(envio);

        var ahora = reloj.GetUtcNow();
        envio.Aplicar(DisparadorEnvio.AsignarARuta, reglas, ahora, "Backoffice", "Despachador");
        envio.Aplicar(DisparadorEnvio.Despachar, reglas, ahora, "Backoffice", hoja.RepartidorId.ToString());

        await unitOfWork.GuardarCambiosAsync(ct);

        return hoja.Paradas.Count(p => p.Activa);
    }
}
