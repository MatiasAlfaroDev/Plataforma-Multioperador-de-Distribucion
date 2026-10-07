using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Configuracion;

public class ZonaCobertura : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Departamento { get; private set; } = string.Empty;
    public bool Activa { get; private set; }

    private ZonaCobertura() { }

    public static ZonaCobertura Crear(
        Guid operadorId,
        string nombre,
        string departamento)
    {
        if (operadorId == Guid.Empty)
            throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre de la zona es obligatorio.", nameof(nombre));

        if (string.IsNullOrWhiteSpace(departamento))
            throw new ArgumentException("El departamento es obligatorio.", nameof(departamento));

        return new ZonaCobertura
        {
            Id = Guid.NewGuid(),
            OperadorId = operadorId,
            Nombre = nombre.Trim(),
            Departamento = departamento.Trim(),
            Activa = true
        };
    }

    public void Desactivar() => Activa = false;
}