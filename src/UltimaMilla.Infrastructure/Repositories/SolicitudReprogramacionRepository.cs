using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class SolicitudReprogramacionRepository(
    AppDbContext db) : ISolicitudReprogramacionRepository
{
    public async Task AgregarAsync(
        SolicitudReprogramacion solicitud,
        CancellationToken ct)
    {
        await db.SolicitudesReprogramacion.AddAsync(solicitud, ct);
    }

    public Task<SolicitudReprogramacion?> ObtenerAsync(
        Guid id,
        CancellationToken ct)
    {
        return db.SolicitudesReprogramacion
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public Task<bool> ExistePendienteAsync(
        Guid envioId,
        CancellationToken ct)
    {
        return db.SolicitudesReprogramacion
            .AnyAsync(s =>
                s.EnvioId == envioId &&
                s.Estado == EstadoSolicitudReprogramacion.Pendiente,
                ct);
    }

    public async Task<IReadOnlyList<SolicitudReprogramacion>>
        ListarPendientesAsync(CancellationToken ct)
    {
        return await db.SolicitudesReprogramacion
            .AsNoTracking()
            .Where(s => s.Estado == EstadoSolicitudReprogramacion.Pendiente)
            .OrderBy(s => s.SolicitadaEn)
            .ToListAsync(ct);
    }
}