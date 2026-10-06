namespace UltimaMilla.Domain.Envios;

/// <summary>
/// Objeto de valor: no tiene identidad propia y se valida al crearse.
/// Se guarda como columnas de la tabla de envíos.
/// </summary>
public sealed record Destinatario
{
    public string Nombre { get; }
    public string? Telefono { get; }

    public Destinatario(string nombre, string? telefono)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del destinatario es obligatorio.", nameof(nombre));

        Nombre = nombre.Trim();
        Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
    }
}
