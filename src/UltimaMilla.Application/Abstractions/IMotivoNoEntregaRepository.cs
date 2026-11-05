using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Abstractions;

public interface IMotivoNoEntregaRepository
{
    Task<MotivoNoEntrega?> ObtenerPorCodigoAsync(
        Guid operadorId,
        Guid versionReglasOperativasId,
        string codigo,
        CancellationToken ct);

    Task<MotivoNoEntrega?> ObtenerPorDescripcionAsync(
        Guid operadorId,
        Guid versionReglasOperativasId,
        string descripcion,
        CancellationToken ct);

    Task<IReadOnlyList<MotivoNoEntrega>> ListarPorVersionAsync(
        Guid operadorId,
        Guid versionReglasOperativasId,
        CancellationToken ct);

    Task AgregarRangoAsync(
        IEnumerable<MotivoNoEntrega> motivos,
        CancellationToken ct);
}