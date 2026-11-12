using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Application.Abstractions;

public interface IRetiroRepository
{
    Task AgregarAsync(Retiro retiro, CancellationToken ct);

    Task<Retiro?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Retiro>> ListarAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct);

    Task<IReadOnlyList<Retiro>> ListarParaPlanificacionAsync(
        Guid operadorId,
        CancellationToken ct);

    Task<IReadOnlyList<Retiro>> ListarParaEntregaComercioAsync(
        Guid operadorId,
        CancellationToken ct);
        
}