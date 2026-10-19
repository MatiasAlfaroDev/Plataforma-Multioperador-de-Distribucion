namespace UltimaMilla.Web.Backoffice.Services;

using System.Globalization;

/// <summary>
/// Formato de pantalla del Backoffice.
/// Se usa es-UY solo para mostrar, no como cultura de la request,
/// porque los input type=number envían punto decimal y el model binding
/// con es-UY lo leería mal.
/// </summary>
public static class Formato
{
    private static readonly CultureInfo Uruguay = CultureInfo.GetCultureInfo("es-UY");
    private static readonly TimeSpan HoraUruguay = TimeSpan.FromHours(-3); // Uruguay no tiene horario de verano desde 2015, así que el desfasaje fijo alcanza y no depende de la zona horaria del contenedor (UTC).

    /// <summary>
    /// Formatea un valor decimal como moneda.
    /// </summary>
    public static string Moneda(decimal valor) => valor.ToString("C2", Uruguay);

    /// <summary>
    /// Formatea un valor decimal como número.
    /// </summary>
    public static string Numero(decimal valor) => valor.ToString("0.###", Uruguay);

    /// <summary>
    /// Formatea una fecha y hora en formato dd/MM/yyyy HH:mm.
    /// </summary>
    public static string Hora(DateTimeOffset momento) => momento.ToOffset(HoraUruguay).ToString("dd/MM/yyyy HH:mm", Uruguay);

    /// <summary>
    /// Convierte la modalidad "Estandar" a "Estándar".
    /// </summary>
    public static string Modalidad(string modalidad) => modalidad == "Estandar" ? "Estándar" : modalidad;
}
