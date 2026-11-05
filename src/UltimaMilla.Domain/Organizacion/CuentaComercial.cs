
using UltimaMilla.Domain.Common;

namespace UltimaMilla.Domain.Organizacion;

public enum EstadoCuentaComercial
{
    Pendiente,
    Confirmada,
    Rechazada
}

/// <summary>
/// Vinculación entre un operador logístico y un comercio.
/// Un comercio puede trabajar con múltiples operadores.
/// </summary>
public class CuentaComercial : AuditableEntity, ITenantEntity
{
    public Guid OperadorId { get; private set; }
    public Guid ComercioId { get; private set; }

    public EstadoCuentaComercial Estado { get; private set; }

    public bool Activa { get; private set; }

    private CuentaComercial() { }

    // Conserva el comportamiento de los datos iniciales existentes.
    public static CuentaComercial Crear(
        Guid id,
        Guid operadorId,
        Guid comercioId)
    {
        return new CuentaComercial
        {
            Id = id,
            OperadorId = operadorId,
            ComercioId = comercioId,
            Estado = EstadoCuentaComercial.Confirmada,
            Activa = true
        };
    }

    // Una solicitud nueva necesita aprobación del operador.
    public static CuentaComercial Solicitar(
        Guid id,
        Guid operadorId,
        Guid comercioId)
    {
        return new CuentaComercial
        {
            Id = id,
            OperadorId = operadorId,
            ComercioId = comercioId,
            Estado = EstadoCuentaComercial.Pendiente,
            Activa = false
        };
    }

    public void Confirmar()
    {
        if (Estado != EstadoCuentaComercial.Pendiente)
            throw new InvalidOperationException(
                "Solo se pueden confirmar solicitudes pendientes.");

        Estado = EstadoCuentaComercial.Confirmada;
        Activa = true;
    }

    public void Rechazar()
    {
        if (Estado != EstadoCuentaComercial.Pendiente)
            throw new InvalidOperationException(
                "Solo se pueden rechazar solicitudes pendientes.");

        Estado = EstadoCuentaComercial.Rechazada;
        Activa = false;
    }

    public void Desactivar()
    {
        Activa = false;
    }
}
