using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class EventoMovilRepository(AppDbContext db) : IEventoMovilRepository
{
    public Task<bool> ExisteAsync(Guid idEvento, CancellationToken ct) =>
        db.EventosMovil.AnyAsync(e => e.Id == idEvento, ct);

    public Task<EventoMovil?> ObtenerEntregaAsync(Guid envioId, CancellationToken ct) =>
        db.EventosMovil.AsNoTracking()
            .Where(e => e.EnvioId == envioId && e.Tipo == TipoEventoMovil.Entregado)
            .OrderBy(e => e.OcurridoEn)
            .FirstOrDefaultAsync(ct);

    public async Task AgregarAsync(EventoMovil evento, CancellationToken ct) =>
        await db.EventosMovil.AddAsync(evento, ct);
}
