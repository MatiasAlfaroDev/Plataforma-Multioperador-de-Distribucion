
namespace UltimaMilla.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> GuardarCambiosAsync(CancellationToken ct);

    Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> operacion,
        CancellationToken ct);
}
