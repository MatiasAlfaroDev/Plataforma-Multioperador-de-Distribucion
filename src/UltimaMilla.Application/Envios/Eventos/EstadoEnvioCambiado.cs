using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Eventos;

/// <summary>
/// Evento de negocio de cada transición de la máquina de estados: uno por cada EventoEnvio nuevo.
/// No lo publica ningún Handler: lo agrega EventosDeEstadoInterceptor al guardar, así ningún caso de uso se lo olvida.
/// Lo consume el Worker para avisar al comercio (RF 28).
/// </summary>
/// <remarks>
/// MassTransit nombra el exchange por namespace y nombre del tipo: no moverlo.
/// </remarks>
public sealed record EstadoEnvioCambiado(
    Guid EventoId,
    Guid EnvioId,
    Guid OperadorId,
    EstadoEnvio EstadoAnterior,
    EstadoEnvio EstadoNuevo,
    DateTimeOffset OcurridoEn);
