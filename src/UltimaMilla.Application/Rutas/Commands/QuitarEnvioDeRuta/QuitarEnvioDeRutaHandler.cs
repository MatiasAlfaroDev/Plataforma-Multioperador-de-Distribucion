using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Rutas.Commands.QuitarEnvioDeRuta;

public sealed class QuitarEnvioDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(
        QuitarEnvioDeRutaCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.HojaDeRutaId == Guid.Empty ||
            command.EnvioId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador, la hoja y el envío son obligatorios.");
        }

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException(
                "Debe identificarse al responsable.");

        var hoja = await hojas.ObtenerAsync(
            command.OperadorId,
            command.HojaDeRutaId,
            ct)
            ?? throw new NoEncontradoException(
                "La hoja de ruta no existe.");

        hoja.VerificarEditable();

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

        var ahora = reloj.GetUtcNow();

        // Libera la parada y actualiza la capacidad asignada.
        hoja.QuitarEnvio(envio, ahora);

        // Restaura EnDeposito o Reprogramado según el origen.
        envio.Aplicar(
            DisparadorEnvio.QuitarDeRuta,
            reglas,
            ahora,
            "Backoffice",
            command.Responsable.Trim());

        // Guarda el estado del envío, la parada y el evento juntos.
        await unitOfWork.GuardarCambiosAsync(ct);
    }
}