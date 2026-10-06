using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Organizacion;

/// <summary>
/// Comercio cliente. Es una identidad GLOBAL: puede trabajar con varios operadores,
/// y cada vínculo operador–comercio es una CuentaComercial.
/// </summary>
public class Comercio : AuditableEntity
{
    public string Rut { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;

    private Comercio() { }

    public static Comercio Crear(Guid id, string rut, string nombre)
    {
        if (string.IsNullOrWhiteSpace(rut))
            throw new ArgumentException("El RUT es obligatorio.", nameof(rut));
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del comercio es obligatorio.", nameof(nombre));

        return new Comercio { Id = id, Rut = rut.Trim(), Nombre = nombre.Trim() };
    }
}
