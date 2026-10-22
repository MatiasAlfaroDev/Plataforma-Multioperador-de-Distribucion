
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Organizacion;

public sealed class Repartidor : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Telefono { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    private Repartidor() { }

    public static Repartidor Crear(
        Guid operadorId,
        string nombre,
        string telefono)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.");

        return new Repartidor
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            Nombre = nombre.Trim(),
            Telefono = telefono?.Trim() ?? string.Empty,
            Activo = true
        };
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;
}
