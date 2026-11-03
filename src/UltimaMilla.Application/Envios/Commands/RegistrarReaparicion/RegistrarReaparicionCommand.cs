namespace UltimaMilla.Application.Envios.Commands.RegistrarReaparicion;

public sealed record RegistrarReaparicionCommand(
    Guid OperadorId,
    Guid EnvioId,
    IReadOnlyList<string> IdentificacionesEscaneadas,
    string Responsable);