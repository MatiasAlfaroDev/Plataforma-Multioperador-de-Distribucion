using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Organizacion;

/// <summary>
/// Vínculo entre un operador y un comercio: "el corazón del multitenancy" (informe, 3.2).
/// Los envíos cuelgan de la cuenta, no del comercio, así un comercio que trabaja
/// con dos operadores no mezcla sus envíos.
/// </summary>
public class CuentaComercial : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public Guid ComercioId { get; private set; }
    public bool Activa { get; private set; }

    private CuentaComercial() { }

    public static CuentaComercial Crear(Guid id, Guid operadorId, Guid comercioId) =>
        new() { Id = id, OperadorId = operadorId, ComercioId = comercioId, Activa = true };

    public void Desactivar() => Activa = false;
}
