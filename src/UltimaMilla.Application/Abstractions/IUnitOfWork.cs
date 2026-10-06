namespace UltimaMilla.Application.Abstractions;

/// <summary>Confirma en una sola transacción todos los cambios pendientes.</summary>
public interface IUnitOfWork
{
    Task<int> GuardarCambiosAsync(CancellationToken ct);
}
