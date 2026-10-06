using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Contrato de acceso a envíos. Lo implementa Infrastructure con EF Core.</summary>
public interface IEnvioRepository
{
    Task AgregarAsync(Envio envio, CancellationToken ct);
    Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct);

    /// <summary>Lista envíos con sus bultos; si viene operadorId, filtra por ese operador.</summary>
    Task<IReadOnlyList<Envio>> ListarAsync(Guid? operadorId, CancellationToken ct);
}
