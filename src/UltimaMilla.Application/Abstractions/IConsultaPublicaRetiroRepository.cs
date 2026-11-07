namespace UltimaMilla.Application.Abstractions;

public interface IConsultaPublicaRetiroRepository
{
    Task<SeguimientoPublicoRetiroDto?> ObtenerAsync(
        Guid operadorId,
        Guid retiroId,
        CancellationToken ct);
}

public sealed record SeguimientoPublicoRetiroDto(
    string Estado,
    DateTimeOffset SolicitadoEn,
    DateOnly FechaSolicitada,
    DateTimeOffset? RetiradoEn);