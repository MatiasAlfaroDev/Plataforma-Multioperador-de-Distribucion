namespace UltimaMilla.Application.Envios.Eventos;

/// <summary>
/// Pide al Worker que le mande al destinatario el enlace público de seguimiento (letra 3.3).
/// Lleva el token en claro porque el envío solo guarda su hash: es la única vez que existe.
/// </summary>
public sealed record EnlaceSeguimientoSolicitado(
    Guid EnvioId,
    Guid OperadorId,
    string Email,
    string NombreDestinatario,
    string ReferenciaExterna,
    string TokenSeguimiento);
