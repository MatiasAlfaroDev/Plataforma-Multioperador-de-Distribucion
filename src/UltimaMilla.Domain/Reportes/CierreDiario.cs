using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Reportes;

/// <summary>Indicadores de un día de operación, calculados con las mismas consultas que los reportes.</summary>
public sealed record IndicadoresDia(
    int EnviosCreados,
    int EntregasCompletas,
    int EntregasParciales,
    int IntentosFallidos,
    int ATiempo,
    int FueraDePlazo,
    decimal? PorcentajePuntualidad)
{
    /// <summary>Porcentaje de resultados de entrega que fueron entregas completas; null si no hubo resultados.</summary>
    public decimal? PorcentajeCumplimiento
    {
        get
        {
            var total = EntregasCompletas + EntregasParciales + IntentosFallidos;
            return total == 0 ? null : Math.Round(EntregasCompletas * 100m / total, 2);
        }
    }
}

/// <summary>
/// Cierre diario de la operación (letra 4: tarea del Worker): foto de los indicadores de un operador para un día.
/// Uno por operador y fecha; si el día se vuelve a cerrar (por ejemplo, con eventos que la app sin conexión
/// sincronizó tarde), se recalcula sobre el mismo registro.
/// </summary>
public sealed class CierreDiario : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }

    /// <summary>Día cerrado, en hora de Uruguay.</summary>
    public DateOnly Fecha { get; private set; }

    public int EnviosCreados { get; private set; }
    public int EntregasCompletas { get; private set; }
    public int EntregasParciales { get; private set; }
    public int IntentosFallidos { get; private set; }
    public decimal? PorcentajeCumplimiento { get; private set; }
    public int ATiempo { get; private set; }
    public int FueraDePlazo { get; private set; }
    public decimal? PorcentajePuntualidad { get; private set; }
    public DateTimeOffset CalculadoEn { get; private set; }

    // Constructor vacío que necesita EF Core para materializar desde la base.
    private CierreDiario() { }

    public static CierreDiario Crear(Guid operadorId, DateOnly fecha, IndicadoresDia indicadores, DateTimeOffset ahora)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        var cierre = new CierreDiario { Id = Guid.NewGuid(), OperadorId = operadorId, Fecha = fecha };
        cierre.Recalcular(indicadores, ahora);
        return cierre;
    }

    public void Recalcular(IndicadoresDia indicadores, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(indicadores);

        EnviosCreados = indicadores.EnviosCreados;
        EntregasCompletas = indicadores.EntregasCompletas;
        EntregasParciales = indicadores.EntregasParciales;
        IntentosFallidos = indicadores.IntentosFallidos;
        PorcentajeCumplimiento = indicadores.PorcentajeCumplimiento;
        ATiempo = indicadores.ATiempo;
        FueraDePlazo = indicadores.FueraDePlazo;
        PorcentajePuntualidad = indicadores.PorcentajePuntualidad;
        CalculadoEn = ahora;
    }
}
