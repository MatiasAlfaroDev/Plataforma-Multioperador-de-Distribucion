using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Abstractions;

public interface IVersionReglasOperativasRepository
{
    Task<VersionReglasOperativas?> ObtenerVigenteAsync(
        Guid operadorId,
        DateTimeOffset fecha,
        CancellationToken ct);

    Task<VersionReglasOperativas?> ObtenerPorIdAsync(
        Guid operadorId,
        Guid versionId,
        CancellationToken ct);
}
