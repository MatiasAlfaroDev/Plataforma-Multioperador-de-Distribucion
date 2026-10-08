using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Contrato de acceso a envíos. Lo implementa Infrastructure con EF Core.</summary>
public interface IEnvioRepository
{
    Task AgregarAsync(Envio envio, CancellationToken ct);

    Task<bool> ExisteReferenciaAsync(
        Guid cuentaComercialId,
        string referenciaExterna,
        CancellationToken ct);

    /// <summary>
    /// Lista los envíos visibles para el tenant actual.
    /// El aislamiento por OperadorId lo aplica globalmente AppDbContext.
    /// </summary>
    Task<IReadOnlyList<Envio>> ListarAsync(CancellationToken ct);
}