using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class CuadroTarifarioRepository(AppDbContext db)
    : ICuadroTarifarioRepository
{
    public Task<CuadroTarifario?> ObtenerAsync(
        Guid operadorId,
        Guid cuadroTarifarioId,
        CancellationToken ct)
    {
        return db.CuadrosTarifarios
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == cuadroTarifarioId &&
                     c.OperadorId == operadorId,
                ct);
    }

    public async Task AgregarAsync(
        CuadroTarifario cuadro,
        CancellationToken ct)
    {
        await db.CuadrosTarifarios.AddAsync(cuadro, ct);
    }

    public Task<bool> ExisteNombreAsync(
        Guid operadorId,
        string nombre,
        CancellationToken ct)
    {
        var nombreNormalizado = nombre.Trim().ToLower();

        return db.CuadrosTarifarios.AnyAsync(
            c => c.OperadorId == operadorId &&
                 c.Nombre.ToLower() == nombreNormalizado,
            ct);
    }
}