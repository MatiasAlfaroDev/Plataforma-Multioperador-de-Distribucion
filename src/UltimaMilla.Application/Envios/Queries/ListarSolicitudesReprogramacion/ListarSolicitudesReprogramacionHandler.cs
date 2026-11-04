using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Envios.Queries.ListarSolicitudesReprogramacion;

public sealed record SolicitudReprogramacionDto(
    Guid Id,
    Guid EnvioId,
    DateTimeOffset SolicitadaEn,
    string Estado);

public sealed class ListarSolicitudesReprogramacionHandler(
    ISolicitudReprogramacionRepository repositorio)
{
    public async Task<IReadOnlyList<SolicitudReprogramacionDto>> Handle(
        CancellationToken ct)
    {
        var solicitudes = await repositorio.ListarPendientesAsync(ct);

        return solicitudes
            .Select(s => new SolicitudReprogramacionDto(
                s.Id,
                s.EnvioId,
                s.SolicitadaEn,
                s.Estado.ToString()))
            .ToList();
    }
}