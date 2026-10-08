namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Permite establecer explícitamente el tenant durante un proceso interno
/// que no dispone de un usuario HTTP autenticado, por ejemplo un Worker.
/// </summary>
public interface ITenantScope
{
    IDisposable UsarTenant(Guid operadorId, Guid? comercioId = null);
}