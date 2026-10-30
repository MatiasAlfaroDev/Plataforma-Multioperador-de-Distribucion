namespace UltimaMilla.Application.Rutas;

/// <summary>
/// Resultado de comparar lo que se escaneó al cargar el vehículo con lo que dice la hoja de ruta (RF 19).
/// </summary>
/// <param name="Cargados">Bultos de la hoja que ya se escanearon.</param>
/// <param name="Faltantes">Bultos de la hoja que todavía no se escanearon.</param>
/// <param name="Sobrantes">Bultos escaneados que no son de esta hoja.</param>
public sealed record ResultadoCarga(
    IReadOnlyList<string> Cargados,
    IReadOnlyList<string> Faltantes,
    IReadOnlyList<string> Sobrantes)
{
    /// <summary>Se puede despachar solo si está todo cargado y no sobra nada.</summary>
    public bool Completa => Faltantes.Count == 0 && Sobrantes.Count == 0;
}

public static class VerificacionDeCarga
{
    /// <summary>
    /// Compara sin importar mayúsculas ni espacios ("ped-300-1 " es "PED-300-1"),
    /// y escanear dos veces el mismo bulto cuenta una sola vez.
    /// </summary>
    public static ResultadoCarga Verificar(IEnumerable<string> esperados, IEnumerable<string> escaneados)
    {
        var deLaHoja = esperados.Select(Normalizar).Where(c => c.Length > 0).ToHashSet();
        var leidos = escaneados.Select(Normalizar).Where(c => c.Length > 0).ToHashSet();

        return new ResultadoCarga(
            deLaHoja.Intersect(leidos).Order().ToList(),
            deLaHoja.Except(leidos).Order().ToList(),
            leidos.Except(deLaHoja).Order().ToList());
    }

    public static string Normalizar(string codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant();
}
