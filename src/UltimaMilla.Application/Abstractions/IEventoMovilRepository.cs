using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Eventos que llegan de la app del repartidor al sincronizar. Lo implementa Infrastructure con EF Core.</summary>
public interface IEventoMovilRepository
{
    /// <summary>True si ya se recibió un evento con ese Id (el que generó el teléfono).</summary>
    Task<bool> ExisteAsync(Guid idEvento, CancellationToken ct);

    /// <summary>La entrega ya registrada para el envío, si hay una.</summary>
    Task<EventoMovil?> ObtenerEntregaAsync(Guid envioId, CancellationToken ct);

    Task AgregarAsync(EventoMovil evento, CancellationToken ct);
}
