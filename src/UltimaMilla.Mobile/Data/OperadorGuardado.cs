using UltimaMilla.Mobile.Models;

namespace UltimaMilla.Mobile.Data;

/// <summary>
/// Operador de la última sesión, guardado en el teléfono para abrir la ruta sin conexión.
/// Solo nombre, color e Id: nada que no sepa ya el repartidor. Se pisa en cada inicio de sesión.
/// </summary>
public static class OperadorGuardado
{
    private const string ClaveId = "operador-sesion-id";
    private const string ClaveNombre = "operador-sesion-nombre";
    private const string ClaveColor = "operador-sesion-color";

    public static void Guardar(OperadorItem operador)
    {
        Preferences.Set(ClaveId, operador.Id.ToString());
        Preferences.Set(ClaveNombre, operador.Nombre);
        Preferences.Set(ClaveColor, operador.ColorPrimario ?? string.Empty);
    }

    public static OperadorItem? Leer()
    {
        if (!Guid.TryParse(Preferences.Get(ClaveId, string.Empty), out var id))
            return null;

        return new OperadorItem(id, Preferences.Get(ClaveNombre, string.Empty), Preferences.Get(ClaveColor, string.Empty));
    }
}
