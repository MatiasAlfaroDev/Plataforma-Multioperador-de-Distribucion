using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.EntregarAviso;

/// <summary>
/// Comando para intentar entregar un aviso pendiente.
/// </summary>
public sealed record EntregarAvisoCommand(Guid EntregaAvisoId);
