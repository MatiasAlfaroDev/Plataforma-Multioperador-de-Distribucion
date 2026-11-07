namespace UltimaMilla.Application.Abstractions;

public interface IReporteRetirosRepository
{
    Task<IReadOnlyList<ConteoRetirosPorEstado>>
        ContarPorEstadoAsync(
            Guid operadorId,
            DateTimeOffset desde,
            DateTimeOffset hastaExclusivo,
            CancellationToken ct);

    Task<decimal> ObtenerTotalTarifadoAsync(
        Guid operadorId,
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        CancellationToken ct);
}

public sealed record ConteoRetirosPorEstado(
    string Estado,
    int Cantidad);