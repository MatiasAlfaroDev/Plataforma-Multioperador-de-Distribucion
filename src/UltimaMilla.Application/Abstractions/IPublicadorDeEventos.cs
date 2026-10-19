namespace UltimaMilla.Application.Abstractions;

/// <summary>Publica un evento de negocio por el outbox (RNF 6.8).</summary>
public interface IPublicadorDeEventos
{
    /// <summary>
    /// Publica un evento de negocio. El evento queda pendiente y se confirma en la misma transacción que <see cref="IUnitOfWork.GuardarCambiosAsync"/>.
    /// Por eso se llama antes de guardar.
    /// </summary>
    Task PublicarAsync<T>(T evento, CancellationToken ct) where T : class;
}
