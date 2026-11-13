namespace UltimaMilla.Domain.Envios;

/// <summary>
/// Objeto de valor: no tiene identidad propia y se valida al crearse.
/// Se guarda como columnas de la tabla de envíos.
/// </summary>
public sealed record Destinatario
{
    public string Nombre { get; }
    public string? Telefono { get; }

    /// <summary>Correo al que se le envía el enlace público de seguimiento (letra 3.3). Opcional.</summary>
    public string? Email { get; }

    public Destinatario(string nombre, string? telefono, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del destinatario es obligatorio.", nameof(nombre));

        // ponytail: chequeo mínimo; el formato completo lo valida FluentValidation en el borde.
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            throw new ArgumentException("El correo del destinatario no es válido.", nameof(email));

        Nombre = nombre.Trim();
        Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }
}
