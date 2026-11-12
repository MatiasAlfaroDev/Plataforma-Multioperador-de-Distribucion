using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Eventos;

/// <summary>
/// Un bulto cambió de estado o no se entregó en una visita (RF 31 y 36): uno por cada EventoBulto nuevo.
/// Igual que EstadoEnvioCambiado, lo publica EventosDeEstadoInterceptor y lo consume el Worker para los avisos v2.
/// </summary>
/// <remarks>MassTransit nombra el exchange por namespace y nombre del tipo: no moverlo.</remarks>
public sealed record EstadoBultoCambiado(
    Guid EventoId,
    Guid EnvioId,
    Guid OperadorId,
    string Identificacion,
    EstadoBulto EstadoAnterior,
    EstadoBulto EstadoNuevo,
    ResultadoBulto Resultado,
    string? Motivo,
    DateTimeOffset OcurridoEn);
