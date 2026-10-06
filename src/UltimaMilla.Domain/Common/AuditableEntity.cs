namespace UltimaMilla.Domain.Common;

/// <summary>
/// Base de las entidades con identidad y auditoría (como la demo Support).
/// Las fechas las completa automáticamente un interceptor de Infrastructure al guardar.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; protected set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset? ActualizadoEn { get; private set; }

    public void MarcarCreado(DateTimeOffset ahora) => CreadoEn = ahora;
    public void MarcarActualizado(DateTimeOffset ahora) => ActualizadoEn = ahora;
}
