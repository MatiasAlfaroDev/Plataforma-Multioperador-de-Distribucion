using MassTransit;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Mensajeria;

/// <summary>
/// Con el bus outbox, el IPublishEndpoint con scope no habla con RabbitMQ.
/// Agrega una fila OutboxMessage al AppDbContext que se confirma en el SaveChanges del UnitOfWork (RNF 6.8).
/// </summary>
public sealed class PublicadorDeEventos(IPublishEndpoint publicador) : IPublicadorDeEventos
{
    /// <summary>
    /// Publica un evento de negocio. El evento queda pendiente y se confirma en la misma transacción que <see cref="IUnitOfWork.GuardarCambiosAsync"/>.
    /// </summary>
    public Task PublicarAsync<T>(T evento, CancellationToken ct) where T : class => publicador.Publish(evento, ct);
}
