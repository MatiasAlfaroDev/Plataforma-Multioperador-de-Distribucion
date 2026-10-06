using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Organizacion;

/// <summary>Operador logístico: el inquilino (tenant) de la plataforma.</summary>
public class Operador : AuditableEntity
{
    public string Nombre { get; private set; } = string.Empty;
    public string ColorPrimario { get; private set; } = string.Empty;
    public string EmailContacto { get; private set; } = string.Empty;

    private Operador() { }

    public static Operador Crear(Guid id, string nombre, string colorPrimario, string emailContacto)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del operador es obligatorio.", nameof(nombre));

        return new Operador
        {
            Id = id,
            Nombre = nombre.Trim(),
            ColorPrimario = colorPrimario,
            EmailContacto = emailContacto
        };
    }
}
