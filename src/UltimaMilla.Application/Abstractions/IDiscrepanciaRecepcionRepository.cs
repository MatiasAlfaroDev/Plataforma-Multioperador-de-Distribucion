using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Discrepancias detectadas al recibir envíos en depósito (RF 10). Lo implementa Infrastructure con EF Core.</summary>
public interface IDiscrepanciaRecepcionRepository
{
    Task AgregarAsync(DiscrepanciaRecepcion discrepancia, CancellationToken ct);

    Task<IReadOnlyList<DiscrepanciaRecepcion>> ListarPorEnvioAsync(
        Guid envioId,
        CancellationToken ct);
}
