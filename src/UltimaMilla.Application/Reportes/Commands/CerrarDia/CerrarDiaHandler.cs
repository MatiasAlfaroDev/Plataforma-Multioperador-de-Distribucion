using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Reportes;

namespace UltimaMilla.Application.Reportes.Commands.CerrarDia;

public sealed record CerrarDiaCommand(Guid OperadorId, DateOnly Fecha);

/// <summary>
/// Cierre diario de la operación (letra 4): calcula los indicadores del día con las mismas consultas de los reportes
/// (envíos, cumplimiento y puntualidad) y los guarda. Idempotente: volver a cerrar el día lo recalcula.
/// El Worker fija el operador en TenantDelMensaje antes de llamarlo.
/// </summary>
public sealed class CerrarDiaHandler(
    IReporteEnviosRepository reportes,
    ICierreDiarioRepository cierres,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    // A diferencia de los reportes (que hoy cortan el día en UTC), el día del cierre es el de Uruguay.
    private static readonly TimeZoneInfo ZonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public async Task<CierreDiario> Handle(CerrarDiaCommand command, CancellationToken ct)
    {
        if (command.OperadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        var inicio = InicioDelDia(command.Fecha);
        var fin = InicioDelDia(command.Fecha.AddDays(1));

        var conteos = await reportes.ContarPorEstadoAsync(command.OperadorId, inicio, fin, ct);
        var cumplimiento = await reportes.ObtenerCumplimientoAsync(command.OperadorId, inicio, fin, ct);
        var puntualidad = await reportes.ObtenerPuntualidadAsync(command.OperadorId, inicio, fin, ct);

        // Cada resultado de entrega cae en una sola zona, así que sumar las zonas da el total del día.
        var indicadores = new IndicadoresDia(
            conteos.Sum(c => c.Cantidad),
            cumplimiento.Zonas.Sum(z => z.Completas),
            cumplimiento.Zonas.Sum(z => z.Parciales),
            cumplimiento.Zonas.Sum(z => z.Fallidas),
            puntualidad.ATiempo,
            puntualidad.FueraDePlazo,
            puntualidad.PorcentajePuntualidad);

        var ahora = reloj.GetUtcNow();
        var cierre = await cierres.ObtenerAsync(command.Fecha, ct);
        if (cierre is null)
        {
            cierre = CierreDiario.Crear(command.OperadorId, command.Fecha, indicadores, ahora);
            await cierres.AgregarAsync(cierre, ct);
        }
        else
        {
            cierre.Recalcular(indicadores, ahora);
        }

        await unitOfWork.GuardarCambiosAsync(ct);
        return cierre;
    }

    /// <summary>Hora local de Uruguay, para que la tarea programada decida si ya pasó la hora del cierre y qué día cerrar.</summary>
    public static DateTime HoraLocal(DateTimeOffset ahora) => TimeZoneInfo.ConvertTime(ahora, ZonaHoraria).DateTime;

    private static DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new(TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), ZonaHoraria), TimeSpan.Zero);
}
