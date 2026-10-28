using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Notificaciones;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

/// <summary>El filtro por operador (AppDbContext) se aplica solo: cada operador ve únicamente sus teléfonos.</summary>
public sealed class DispositivoMovilRepository(AppDbContext db) : IDispositivoMovilRepository
{
    public Task<DispositivoMovil?> ObtenerPorTokenAsync(string token, CancellationToken ct) =>
        db.DispositivosMoviles.FirstOrDefaultAsync(d => d.Token == token, ct);

    public async Task<IReadOnlyList<DispositivoMovil>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        await db.DispositivosMoviles
            .Where(d => d.UsuarioId == usuarioId)
            .ToListAsync(ct);

    public async Task AgregarAsync(DispositivoMovil dispositivo, CancellationToken ct) =>
        await db.DispositivosMoviles.AddAsync(dispositivo, ct);

    public void Eliminar(DispositivoMovil dispositivo) =>
        db.DispositivosMoviles.Remove(dispositivo);
}
