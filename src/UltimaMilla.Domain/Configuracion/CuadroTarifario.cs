using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Configuracion;

public class CuadroTarifario : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    private CuadroTarifario() { }

    public static CuadroTarifario Crear(
        Guid operadorId,
        string nombre)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException(
                "El operador es obligatorio.",
                nameof(operadorId));

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException(
                "El nombre del cuadro tarifario es obligatorio.",
                nameof(nombre));

        return new CuadroTarifario
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            Nombre = nombre.Trim(),
            Activo = true
        };
    }

    public void Desactivar() => Activo = false;
}