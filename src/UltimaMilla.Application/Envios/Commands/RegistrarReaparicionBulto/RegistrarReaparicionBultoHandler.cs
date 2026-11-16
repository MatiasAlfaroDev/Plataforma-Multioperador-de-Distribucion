using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;

namespace UltimaMilla.Application.Envios.Commands.RegistrarReaparicionBulto;

/// <summary>Reaparece un bulto suelto de un envío que no está extraviado.</summary>
public sealed class RegistrarReaparicionBultoHandler(
    IEnvioRepository envios,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(
        RegistrarReaparicionBultoCommand command,
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

        envio.ReaparecerBulto(
            command.Identificacion,
            command.Responsable?.Trim() ?? string.Empty,
            reloj.GetUtcNow());

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
