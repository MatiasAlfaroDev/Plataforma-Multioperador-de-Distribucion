using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

public interface ISolicitudReprogramacionRepository
{
    Task AgregarAsync(
        SolicitudReprogramacion solicitud,
        CancellationToken ct);

    Task<SolicitudReprogramacion?> ObtenerAsync(
        Guid id,
        CancellationToken ct);

    Task<bool> ExistePendienteAsync(
        Guid envioId,
        CancellationToken ct);

    Task<IReadOnlyList<SolicitudReprogramacion>> ListarPendientesAsync(
        CancellationToken ct);
}