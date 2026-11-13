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
    DateTimeOffset OcurridoEn,
    BultoDelAviso? Bulto = null);

/// <summary>El bulto que originó el aviso (RF 36), solo en avisos de bulto.</summary>
public sealed record BultoDelAviso(string Identificacion, string EstadoAnterior, string EstadoNuevo, string? Motivo);
