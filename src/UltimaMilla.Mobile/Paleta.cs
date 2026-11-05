namespace UltimaMilla.Mobile;

/// <summary>
/// Colores de Colors.xaml para lo que se resuelve en C# (estado de un envío, por ejemplo).
/// En modo oscuro usa la variante "Oscuro" de la clave si existe.
/// </summary>
public static class Paleta
{
    public static Color De(string clave)
    {
        var app = Application.Current!;
        if (app.RequestedTheme == AppTheme.Dark && app.Resources.TryGetValue(clave + "Oscuro", out var oscuro))
            return (Color)oscuro;
        return (Color)app.Resources[clave];
    }
}
