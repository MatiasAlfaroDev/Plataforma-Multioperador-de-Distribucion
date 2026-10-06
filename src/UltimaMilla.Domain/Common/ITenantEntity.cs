namespace UltimaMilla.Domain.Common;

/// <summary>
/// Marca las entidades que pertenecen a un operador (inquilino), según el ADR-002.
/// Para el 15/10 los filtros globales de EF Core usarán esta interfaz.
/// </summary>
public interface ITenantEntity
{
    Guid OperadorId { get; }
}
