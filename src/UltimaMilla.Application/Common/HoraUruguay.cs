namespace UltimaMilla.Application.Common;

/// <summary>
/// Hora y día de la operación: los operadores trabajan en Uruguay, así que los días de los reportes, del cierre diario,
/// del tablero y de las hojas de ruta se cortan en America/Montevideo y no en UTC.
/// </summary>
public static class HoraUruguay
{
    public const string IdZona = "America/Montevideo";

    /// <summary>
    /// La zona del sistema; si la imagen no trae tzdata, UTC-3 fijo (Uruguay no tiene horario de verano desde 2015),
    /// así ningún tipo que la use falla al inicializarse.
    /// </summary>
    public static readonly TimeZoneInfo Zona = Resolver();

    /// <summary>Fecha y hora local de Uruguay de un instante.</summary>
    public static DateTime ALocal(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Zona).DateTime;

    /// <summary>Día de Uruguay al que pertenece un instante.</summary>
    public static DateOnly Fecha(DateTimeOffset instante) => DateOnly.FromDateTime(ALocal(instante));

    /// <summary>Instante en que empieza ese día en Uruguay.</summary>
    public static DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new(TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), Zona), TimeSpan.Zero);

    private static TimeZoneInfo Resolver()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IdZona);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(IdZona, TimeSpan.FromHours(-3), "Uruguay (UTC-3)", "UYT");
        }
    }
}
