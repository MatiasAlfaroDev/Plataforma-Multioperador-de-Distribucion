using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.RegistrarReaparicion;

public sealed class RegistrarReaparicionHandler(
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(
        RegistrarReaparicionCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.EnvioId == Guid.Empty)
        {
            throw new ArgumentException(
                "El operador y el envío son obligatorios.");
        }

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException(
                "Debe identificarse al responsable.");

        if (command.IdentificacionesEscaneadas is null ||
            command.IdentificacionesEscaneadas.Count == 0)
        {
            throw new ArgumentException(
                "Debe escanearse al menos un bulto.");
        }

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException(
                "El envío no existe.");

        if (envio.OperadorId != command.OperadorId)
            throw new InvalidOperationException(
                "El envío pertenece a otro operador.");

        if (envio.Estado != EstadoEnvio.Extraviado)
            throw new InvalidOperationException(
                "Solo puede registrarse la reaparición de un envío extraviado.");

        // Verificamos todos los bultos físicos recuperados.
        var esperados = envio.Bultos
            .Select(b => b.Identificacion)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var escaneados = command.IdentificacionesEscaneadas
            .Select(codigo => codigo?.Trim() ?? string.Empty)
            .ToList();

        if (escaneados.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Las identificaciones escaneadas no pueden estar vacías.");

        if (escaneados.Count != escaneados
                .Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new InvalidOperationException(
                "Hay identificaciones escaneadas repetidas.");
        }

        var recibidos = escaneados
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!esperados.SetEquals(recibidos))
        {
            throw new InvalidOperationException(
                "Los bultos escaneados no coinciden con los declarados en el envío.");
        }

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene reglas operativas asignadas.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            command.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        envio.Aplicar(
            DisparadorEnvio.Reaparecer,
            reglas,
            reloj.GetUtcNow(),
            "Deposito",
            command.Responsable.Trim());

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}