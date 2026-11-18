using StackExchange.Redis;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Reportes.Commands.CerrarDia;

namespace UltimaMilla.Worker.Tareas;

/// <summary>
/// Cierre diario y recálculo de indicadores (letra 4: tareas del Worker). Al arrancar y después cada hora vuelve a
/// cerrar, para cada operador, los últimos días ya terminados en Uruguay (CierreDiario:DiasARecalcular, 7 por defecto).
/// Así el día de ayer queda cerrado en la primera hora del día siguiente, una caída del Worker o una vuelta cortada a la
/// mitad se recuperan en la vuelta siguiente, y los eventos que la app sin conexión sincroniza tarde (ADR-003) entran en
/// el cierre de su día. CerrarDiaHandler es idempotente (un cierre por operador y fecha), así que repetir no duplica.
/// </summary>
public sealed class CierreDiarioDeOperacion(
    IServiceScopeFactory scopes,
    IConnectionMultiplexer redis,
    TimeProvider reloj,
    IConfiguration configuracion,
    ILogger<CierreDiarioDeOperacion> logger) : BackgroundService
{
    private static readonly TimeSpan Periodo = TimeSpan.FromHours(1);

    // Lock corto: solo evita que dos réplicas corran la misma vuelta a la vez; no marca nada como cerrado.
    private static readonly TimeSpan VencimientoLock = TimeSpan.FromMinutes(10);
    private const string ClaveLock = "ultimamilla:lock:cierre-diario";

    private readonly int diasARecalcular = Math.Max(1, configuracion.GetValue("CierreDiario:DiasARecalcular", 7));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Periodo, reloj);

        // Primera vuelta al arrancar, sin esperar el primer tic: es la que pone al día lo que quedó sin cerrar.
        do
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
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EjecutarVueltaAsync(CancellationToken ct)
    {
        if (!await redis.GetDatabase().StringSetAsync(ClaveLock, Environment.MachineName, VencimientoLock, When.NotExists))
        {
            logger.LogDebug("Otra réplica tiene el lock del cierre diario.");
            return;
        }

        // Desde ayer hacia atrás; el día en curso no se cierra.
        var ayer = HoraUruguay.Fecha(reloj.GetUtcNow()).AddDays(-1);
        var fechas = Enumerable.Range(0, diasARecalcular).Select(i => ayer.AddDays(-i)).ToList();

        await using var scope = scopes.CreateAsyncScope();
        var operadores = await scope.ServiceProvider.GetRequiredService<IOperadorRepository>().ListarAsync(ct);

        foreach (var operador in operadores)
        {
            foreach (var fecha in fechas)
            {
                try
                {
                    // Un scope nuevo por cierre: AppDbContext limpio y TenantDelMensaje del operador.
                    await using var scopeCierre = scopes.CreateAsyncScope();
                    scopeCierre.ServiceProvider.GetRequiredService<TenantDelMensaje>().Fijar(operador.Id);

                    // Hay que fijar el operador ANTES del Handler (si no, los filtros por operador no devuelven nada y TenantSaveChangesInterceptor rechaza el guardado).
                    await scopeCierre.ServiceProvider.GetRequiredService<CerrarDiaHandler>()
                        .Handle(new CerrarDiaCommand(operador.Id, fecha), ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    // Se sigue con el resto; la vuelta siguiente vuelve a intentar este día.
                    logger.LogError(ex, "Falló el cierre del {Fecha} del operador {OperadorId}.", fecha, operador.Id);
                }
            }
        }

        logger.LogInformation(
            "Cierre diario: recalculados del {Desde} al {Hasta} para {Operadores} operadores.",
            fechas[^1], ayer, operadores.Count);
    }
}
