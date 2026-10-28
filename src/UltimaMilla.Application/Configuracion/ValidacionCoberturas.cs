using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Configuracion;

/// <summary>
/// Regla entre zonas: una localidad (o un sector) no puede quedar cubierta por dos zonas
/// activas del mismo operador en el mismo departamento. La hoja de ruta necesita que cada
/// envío caiga en una única zona. La usan el alta y la edición de zonas.
/// </summary>
public static class ValidacionCoberturas
{
    public static void ValidarSinSuperposicion(
        ZonaCobertura zona,
        IEnumerable<ZonaCobertura> otrasZonas)
    {
        foreach (var existente in otrasZonas)
        {
            // Al editar, la zona aparece también en la lista: no se compara consigo misma.
            if (existente.Id == zona.Id)
                continue;

            if (!string.Equals(
                    existente.Departamento,
                    zona.Departamento,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var cobertura in zona.Localidades)
            {
                foreach (var otra in existente.Localidades)
                {
                    if (!string.Equals(
                            cobertura.Nombre,
                            otra.Nombre,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    var superpuestas =
                        cobertura.Sector is null ||
                        otra.Sector is null ||
                        string.Equals(
                            cobertura.Sector,
                            otra.Sector,
                            StringComparison.OrdinalIgnoreCase);

                    if (superpuestas)
                        throw new InvalidOperationException(
                            $"La cobertura de '{cobertura.Nombre}' " +
                            $"se superpone con la zona '{existente.Nombre}'.");
                }
            }
        }
    }
}
