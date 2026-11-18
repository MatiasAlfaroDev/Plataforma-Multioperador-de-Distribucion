using StackExchange.Redis;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Reportes.Commands.CerrarDia;

namespace UltimaMilla.Worker.Tareas;

/// <summary>
/// Cierre diario de la operación (letra 4: el Worker hace el recálculo de indicadores y el cierre diario).
/// Cada 15 minutos mira si ya pasó la hora del cierre (CierreDiario:Hora, hora de Uruguay, 2 por defecto) y, si el día
/// anterior no se cerró todavía, guarda los indicadores de cada operador. Si el Worker estuvo caído a esa hora, lo cierra
/// en la primera vuelta después de arrancar.
/// </summary>
public sealed class CierreDiarioDeOperacion(
    IServiceScopeFactory scopes,
    IConnectionMultiplexer redis,
    TimeProvider reloj,
    IConfiguration configuracion,
    ILogger<CierreDiarioDeOperacion> logger) : BackgroundService
{
    private static readonly TimeSpan Periodo = TimeSpan.FromMinutes(15);

    // El lock lleva la fecha y dura más que un día: además de evitar que dos réplicas corran juntas,
    // marca el día como cerrado y las vueltas siguientes no lo repiten.
    private static readonly TimeSpan VencimientoLock = TimeSpan.FromHours(26);

    private readonly int horaCierre = configuracion.GetValue("CierreDiario:Hora", 2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Periodo, reloj);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await EjecutarVueltaAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falló la vuelta del cierre diario.");
            }
        }
    }

    private async Task EjecutarVueltaAsync(CancellationToken ct)
    {
        var local = CerrarDiaHandler.HoraLocal(reloj.GetUtcNow());
        if (local.Hour < horaCierre)
            return;

        var fecha = DateOnly.FromDateTime(local).AddDays(-1);
        var claveLock = $"ultimamilla:lock:cierre-diario:{fecha:yyyy-MM-dd}";
        var db = redis.GetDatabase();

        if (!await db.StringSetAsync(claveLock, Environment.MachineName, VencimientoLock, When.NotExists))
        {
            logger.LogDebug("El cierre del {Fecha} ya se hizo o lo tiene otra réplica.", fecha);
            return;
        }

        var fallidos = 0;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var operadores = await scope.ServiceProvider.GetRequiredService<IOperadorRepository>().ListarAsync(ct);

            foreach (var operador in operadores)
            {
                try
                {
                    // Un scope nuevo por operador: AppDbContext y TenantDelMensaje.
                    await using var scopeOperador = scopes.CreateAsyncScope();
                    scopeOperador.ServiceProvider.GetRequiredService<TenantDelMensaje>().Fijar(operador.Id);

                    // Hay que fijar el operador ANTES del Handler (si no, los filtros por operador no devuelven nada y TenantSaveChangesInterceptor rechaza el guardado).
                    var cierre = await scopeOperador.ServiceProvider.GetRequiredService<CerrarDiaHandler>()
                        .Handle(new CerrarDiaCommand(operador.Id, fecha), ct);

                    logger.LogInformation(
                        "Cierre del {Fecha} del operador {OperadorId}: {Envios} envíos, {Completas} entregas completas, {Fallidos} intentos fallidos.",
                        fecha, operador.Id, cierre.EnviosCreados, cierre.EntregasCompletas, cierre.IntentosFallidos);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    fallidos++;
                    logger.LogError(ex, "Falló el cierre del {Fecha} del operador {OperadorId}.", fecha, operador.Id);
                }
            }
        }
        catch
        {
            fallidos++;
            throw;
        }
        finally
        {
            // Si algo falló se suelta el lock y la vuelta siguiente lo reintenta; el cierre es idempotente (recalcula).
            if (fallidos > 0)
                await db.KeyDeleteAsync(claveLock);
        }
    }
}
