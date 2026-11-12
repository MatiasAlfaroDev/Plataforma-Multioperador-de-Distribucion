using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.CrearSuscripcion;

/// <summary>
/// Comando para crear una suscripción a avisos.
/// El comercio no manda la cuenta (sale de la sesión); el administrador del operador sí.
/// </summary>
public sealed record CrearSuscripcionCommand(
    Guid? CuentaComercialId,
    string Url,
    IReadOnlyList<string> TiposEvento,
    int VersionContrato = 1);
