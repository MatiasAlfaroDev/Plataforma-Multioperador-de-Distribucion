using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface IOperadorRepository
{
    Task<IReadOnlyList<Operador>> ListarAsync(CancellationToken ct);
}
