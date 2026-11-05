using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.RegistrarAvisos;

/// <summary>
/// Comando para registrar avisos a partir de un evento de envío.
/// Lo invoca el Worker al consumir un evento del envío (RF 28).
/// </summary>
public sealed record RegistrarAvisosCommand(
    Guid EnvioId,
    string TipoEvento,
    string ClaveEvento,
    DateTimeOffset OcurridoEn);
