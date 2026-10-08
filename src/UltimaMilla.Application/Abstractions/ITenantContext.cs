namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Representa el inquilino (tenant) del usuario autenticado actualmente.
/// Permite que Application conozca el operador/comercio actual
/// sin depender de ASP.NET Core ni de HttpContext.
/// </summary>
public interface ITenantContext
{
    Guid? OperadorId { get; }

    Guid? ComercioId { get; }

    bool TieneOperador => OperadorId.HasValue;

    bool TieneComercio => ComercioId.HasValue;
}