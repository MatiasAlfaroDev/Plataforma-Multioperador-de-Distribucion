using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Application.Abstractions;

public interface IEventoRetiroMovilRepository
{
    Task<bool> ExisteAsync(
        Guid idEvento,
        CancellationToken ct);

    Task AgregarAsync(
        EventoRetiroMovil evento,
        CancellationToken ct);
}