using System.Globalization;

namespace UltimaMilla.Web.Backoffice.Services;

/// <summary>
/// Pasa las coberturas y franjas de una zona entre el texto del formulario
/// (una por línea) y los records que viajan a la API.
/// Coberturas: "Localidad" o "Localidad | Sector". Franjas: "HH:mm-HH:mm".
/// </summary>
public static class FormatoZonas
{
    public static List<CoberturaZonaRequest> LeerCoberturas(string? texto, List<string> errores)
    {
        var resultado = new List<CoberturaZonaRequest>();

        foreach (var linea in SepararLineas(texto))
        {
            var partes = linea.Split('|', 2);
            var localidad = partes[0].Trim();
            var sector = partes.Length == 2 ? partes[1].Trim() : null;

            if (string.IsNullOrWhiteSpace(localidad) ||
                partes.Length == 2 && string.IsNullOrWhiteSpace(sector))
            {
                errores.Add($"Cobertura inválida: '{linea}'. Usá 'Localidad' o 'Localidad | Sector'.");
                continue;
            }

            resultado.Add(new CoberturaZonaRequest(localidad, sector));
        }

        if (resultado.Count == 0)
            errores.Add("Agregá al menos una cobertura.");

        return resultado;
    }

    public static List<FranjaZonaRequest> LeerFranjas(string? texto, List<string> errores)
    {
        var resultado = new List<FranjaZonaRequest>();

        foreach (var linea in SepararLineas(texto))
        {
            var partes = linea.Split('-', 2);

            if (partes.Length != 2 ||
                !TimeOnly.TryParseExact(partes[0].Trim(), "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var desde) ||
                !TimeOnly.TryParseExact(partes[1].Trim(), "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var hasta) ||
                desde >= hasta)
            {
                errores.Add($"Franja inválida: '{linea}'. Usá HH:mm-HH:mm.");
                continue;
            }

            resultado.Add(new FranjaZonaRequest(desde, hasta));
        }

        if (resultado.Count == 0)
            errores.Add("Agregá al menos una franja.");

        return resultado;
    }

    public static string EscribirCoberturas(IEnumerable<CoberturaZonaRequest> coberturas) =>
        string.Join(Environment.NewLine, coberturas.Select(c =>
            string.IsNullOrWhiteSpace(c.Sector) ? c.Localidad : $"{c.Localidad} | {c.Sector}"));

    public static string EscribirFranjas(IEnumerable<FranjaZonaRequest> franjas) =>
        string.Join(Environment.NewLine, franjas.Select(f =>
            $"{f.HoraDesde.ToString("HH:mm", CultureInfo.InvariantCulture)}-" +
            $"{f.HoraHasta.ToString("HH:mm", CultureInfo.InvariantCulture)}"));

    private static IEnumerable<string> SepararLineas(string? texto) =>
        (texto ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
}
