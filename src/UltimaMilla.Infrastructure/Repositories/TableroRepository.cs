using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Repositories;

public sealed class TableroRepository(AppDbContext db) : ITableroRepository
{
    // Lo que todavía puede entregarse en la ventana: después de esto el envío ya tuvo su resultado.
    private static readonly EstadoEnvio[] SinEntregar =
        [EstadoEnvio.Admitido, EstadoEnvio.EnDeposito, EstadoEnvio.AsignadoARuta, EstadoEnvio.EnTransito];

    public async Task<IReadOnlyList<ConteoEstado>> ContarPorEstadoAsync(CancellationToken ct) =>
        await db.Envios
            .AsNoTracking()
            .GroupBy(e => e.Estado)
            .Select(g => new ConteoEstado(g.Key, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PosicionFlotaDto>> UltimasPosicionesAsync(DateTimeOffset desde, CancellationToken ct)
    {
        var recientes = await db.PosicionesRepartidor
            .AsNoTracking()
            .Where(p => p.RegistradaEn >= desde)
            .Join(db.Repartidores, p => p.RepartidorId, r => r.Id,
                (p, r) => new PosicionFlotaDto(r.Id, r.Nombre, p.Latitud, p.Longitud, p.RegistradaEn))
            .ToListAsync(ct);

        // ponytail: la última por repartidor se elige en memoria; son dos horas de posiciones de un operador.
        return recientes
            .GroupBy(p => p.RepartidorId)
            .Select(g => g.MaxBy(p => p.RegistradaEn)!)
            .ToList();
    }

    public async Task<IReadOnlyList<EnvioConVentana>> PendientesConVentanaHastaAsync(DateOnly hasta, CancellationToken ct)
    {
        var envios = await db.Envios
            .AsNoTracking()
            .Where(e => SinEntregar.Contains(e.Estado) &&
                        e.FechaEntregaComprometida != null &&
                        e.FechaEntregaComprometida <= hasta &&
                        e.HoraEntregaDesde != null &&
                        e.HoraEntregaHasta != null)
            .ToListAsync(ct);

        return envios
            .Select(e => new EnvioConVentana(
                e.Id, e.ReferenciaExterna, e.Estado, e.Destinatario.Nombre, e.Direccion.ToString(),
                e.FechaEntregaComprometida!.Value, e.HoraEntregaDesde!.Value, e.HoraEntregaHasta!.Value))
            .ToList();
    }
}
