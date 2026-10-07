namespace UltimaMilla.Mobile.Models;

/// <summary>Lo que devuelve GET /api/operadores.</summary>
public sealed record OperadorItem(Guid Id, string Nombre, string ColorPrimario)
{
    /// <summary>Color de marca del operador, listo para usar en la pantalla.</summary>
    public Color ColorMarca => Color.FromArgb(string.IsNullOrWhiteSpace(ColorPrimario) ? "#888888" : ColorPrimario);
}
