using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Configuracion;

public class MotivoNoEntrega : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public Guid VersionReglasOperativasId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool RequiereDevolucion { get; private set; }

    private MotivoNoEntrega() { }

    public static MotivoNoEntrega Crear(
        Guid operadorId,
        Guid versionReglasOperativasId,
        string codigo,
        string descripcion,
        bool requiereDevolucion)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.");

        if (versionReglasOperativasId == Guid.Empty)
            throw new ArgumentException("La versión de reglas operativas es obligatoria.",
                nameof(versionReglasOperativasId));

        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El código es obligatorio.");

        if (string.IsNullOrWhiteSpace(descripcion))
            throw new ArgumentException("La descripción es obligatoria.");

        return new MotivoNoEntrega
        {
            Id = Guid.NewGuid(),
            VersionReglasOperativasId = versionReglasOperativasId,
            OperadorId = operadorId,
            Codigo = codigo.Trim(),
            Descripcion = descripcion.Trim(),
            RequiereDevolucion = requiereDevolucion
        };
    }
}