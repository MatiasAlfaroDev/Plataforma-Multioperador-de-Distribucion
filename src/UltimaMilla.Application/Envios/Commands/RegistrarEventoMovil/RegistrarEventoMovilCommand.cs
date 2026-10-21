namespace UltimaMilla.Application.Envios.Commands.RegistrarEventoMovil;

/// <summary>
/// Un hecho que la app del repartidor sincroniza: Entregado (con receptor) o NoEntregado (con motivo).
/// IdEvento lo genera el teléfono y es la clave de idempotencia.
/// FechaHora es cuándo pasó en la calle, no cuándo se sincronizó.
/// </summary>
public sealed record RegistrarEventoMovilCommand(
    Guid EnvioId,
    Guid IdEvento,
    string Tipo,
    string? Motivo,
    string? Receptor,
    DateTimeOffset FechaHora);
