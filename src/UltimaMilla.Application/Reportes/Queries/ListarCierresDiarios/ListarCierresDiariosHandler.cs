using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Reportes.Queries.ListarCierresDiarios;

public sealed record CierreDiarioDto(
    DateOnly Fecha,
    int EnviosCreados,
    int EntregasCompletas,
    int EntregasParciales,
    int IntentosFallidos,
    decimal? PorcentajeCumplimiento,
    int ATiempo,
    int FueraDePlazo,
    decimal? PorcentajePuntualidad,
    DateTimeOffset CalculadoEn);

/// <summary>Cierres diarios del operador de la sesión en un rango de fechas.</summary>
public sealed class ListarCierresDiariosHandler(ICierreDiarioRepository cierres)
{
    /// <summary>Tope del rango: un año de cierres alcanza para cualquier comparación.</summary>
    public const int MaximoDias = 366;

    public async Task<IReadOnlyList<CierreDiarioDto>> Handle(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde)
            throw new ArgumentException("Rango de fechas no válido.");

        if (hasta.DayNumber - desde.DayNumber >= MaximoDias)
            throw new ArgumentException($"El rango no puede superar los {MaximoDias} días.");

        var lista = await cierres.ListarAsync(desde, hasta, ct);
        return lista.Select(c => new CierreDiarioDto(
            c.Fecha, c.EnviosCreados, c.EntregasCompletas, c.EntregasParciales, c.IntentosFallidos,
            c.PorcentajeCumplimiento, c.ATiempo, c.FueraDePlazo, c.PorcentajePuntualidad, c.CalculadoEn)).ToList();
    }
}
