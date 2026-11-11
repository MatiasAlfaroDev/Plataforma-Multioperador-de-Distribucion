using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Retiros;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class EventoRetiroMovilRepository(
    AppDbContext db) : IEventoRetiroMovilRepository
{
    public Task<bool> ExisteAsync(
        Guid idEvento,
        CancellationToken ct)
    {
        return db.Set<EventoRetiroMovil>()
            .AnyAsync(e => e.Id == idEvento, ct);
    }

    public async Task AgregarAsync(
        EventoRetiroMovil evento,
        CancellationToken ct)
    {
        await db.Set<EventoRetiroMovil>()
            .AddAsync(evento, ct);
    }
}