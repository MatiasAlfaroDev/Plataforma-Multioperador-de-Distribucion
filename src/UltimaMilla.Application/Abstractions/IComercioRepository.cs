using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Application.Abstractions;

public interface IComercioRepository
{
    Task<Comercio?> ObtenerAsync(Guid id, CancellationToken ct);
}
