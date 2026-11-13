using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Plataforma;

/// <summary>
/// Implementación propia (aclaración 3.2, justificada en ADR-006): una consulta por (bandera, operador)
/// con las banderas del operador en Redis. Al cambiar una se borra la entrada, así que la próxima lectura
/// de cualquier réplica ya ve el valor nuevo; si el borrado fallara, la expiración acota la demora.
/// </summary>
public sealed class BanderasService(AppDbContext db, ICacheService cache) : IBanderas
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(15);

    public static string Clave(Guid operadorId) => $"banderas:{operadorId}";

    public async Task<bool> EstaHabilitadaAsync(string bandera, Guid operadorId, CancellationToken ct)
    {
        var deshabilitadas = await cache.ObtenerAsync<List<string>>(Clave(operadorId), ct);

        if (deshabilitadas is null)
        {
            deshabilitadas = await db.BanderasFuncionalidad
                .AsNoTracking()
                .Where(b => b.OperadorId == operadorId && !b.Habilitada)
                .Select(b => b.Nombre)
                .ToListAsync(ct);

            await cache.GuardarAsync(Clave(operadorId), deshabilitadas, Vigencia, ct);
        }

        // Sin fila, habilitada: es el comportamiento que había antes de las banderas.
        return !deshabilitadas.Contains(bandera);
    }
}
