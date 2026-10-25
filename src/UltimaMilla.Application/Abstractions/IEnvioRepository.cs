using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Abstractions;

/// <summary>Contrato de acceso a envíos. Lo implementa Infrastructure con EF Core.</summary>
public interface IEnvioRepository
{
    Task AgregarAsync(Envio envio, CancellationToken ct);
    Task<Envio?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteReferenciaAsync(Guid cuentaComercialId, string referenciaExterna, CancellationToken ct);

    /// <summary>
    /// Lista los envíos de un operador y, opcionalmente,
    /// de una cuenta comercial específica.
    /// </summary>
    Task<IReadOnlyList<Envio>> ListarAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct);
}
