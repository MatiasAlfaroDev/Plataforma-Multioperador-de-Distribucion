using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.ReenviarAviso;

/// <summary>
/// Comando para reenviar un aviso fallido.
/// </summary>
public sealed record ReenviarAvisoCommand(Guid EntregaAvisoId);
