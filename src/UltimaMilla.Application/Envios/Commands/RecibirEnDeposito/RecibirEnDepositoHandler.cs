using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Commands.RecibirEnDeposito;

public sealed class RecibirEnDepositoHandler(
    IEnvioRepository envios,
    IVersionReglasOperativasRepository reglasRepository,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(
        RecibirEnDepositoCommand command,
        CancellationToken ct)
    {
        if (command.EnvioId == Guid.Empty)
            throw new ArgumentException("El envío es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.Responsable))
            throw new ArgumentException("Debe identificarse al responsable.");

        if (command.IdentificacionesEscaneadas is null ||
            command.IdentificacionesEscaneadas.Count == 0)
            throw new ArgumentException("Debe escanearse al menos un bulto.");

        var envio = await envios.ObtenerAsync(command.EnvioId, ct)
            ?? throw new NoEncontradoException("El envío no existe.");

        if (envio.Estado != EstadoEnvio.Admitido)
            throw new InvalidOperationException(
                "Solo se pueden recibir envíos en estado Admitido.");

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
            throw new InvalidOperationException(
                "Hay identificaciones escaneadas repetidas.");

        var recibidos = escaneados
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!esperados.SetEquals(recibidos))
            throw new InvalidOperationException(
                "Los bultos escaneados no coinciden con los declarados en el envío.");

        if (envio.VersionReglasOperativasId is not Guid versionId)
            throw new InvalidOperationException(
                "El envío no tiene una versión de reglas operativas asignada.");

        var reglas = await reglasRepository.ObtenerPorIdAsync(
            envio.OperadorId,
            versionId,
            ct)
            ?? throw new NoEncontradoException(
                "No se encontraron las reglas operativas del envío.");

        envio.Aplicar(
            DisparadorEnvio.RecibirEnDeposito,
            reglas,
            reloj.GetUtcNow(),
            "Backoffice",
            command.Responsable.Trim());

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}