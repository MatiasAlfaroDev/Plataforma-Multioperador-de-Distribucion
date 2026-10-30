
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Organizacion;

public sealed class Repartidor : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Telefono { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    /// <summary>
    /// Usuario con el que el repartidor inicia sesión en la app (perfil Repartidor).
    /// Null mientras no tenga cuenta: puede figurar en hojas de ruta pero no usar la app.
    /// </summary>
    public Guid? UsuarioId { get; private set; }

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

    /// <summary>Le asigna la cuenta con la que va a usar la app.</summary>
    public void VincularUsuario(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));

        if (UsuarioId is not null && UsuarioId != usuarioId)
            throw new InvalidOperationException(
                "El repartidor ya está vinculado a otro usuario. Desvinculalo primero.");

        UsuarioId = usuarioId;
    }

    public void DesvincularUsuario() => UsuarioId = null;

    public void Activar() => Activo = true;
}
