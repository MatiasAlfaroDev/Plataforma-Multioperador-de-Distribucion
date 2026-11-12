
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.UnitTests;

public sealed class RetirosEnMemoria(
    List<Retiro> retiros) : IRetiroRepository
{
    public Task AgregarAsync(
        Retiro retiro,
        CancellationToken ct)
    {
        retiros.Add(retiro);
        return Task.CompletedTask;
    }

    public Task<Retiro?> ObtenerAsync(
        Guid id,
        CancellationToken ct)
    {
        return Task.FromResult(
            retiros.FirstOrDefault(r => r.Id == id));
    }

    public Task<IReadOnlyList<Retiro>> ListarAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct)
    {
        IReadOnlyList<Retiro> resultado = retiros
            .Where(r =>
                r.OperadorId == operadorId &&
                (!cuentaComercialId.HasValue ||
                 r.CuentaComercialId == cuentaComercialId.Value))
            .OrderByDescending(r => r.SolicitadoEn)
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<Retiro>> ListarParaPlanificacionAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        IReadOnlyList<Retiro> resultado = retiros
            .Where(r =>
                r.OperadorId == operadorId &&
                r.Estado == EstadoRetiro.Solicitado &&
                r.HojaDeRutaId == null)
            .OrderBy(r => r.FechaSolicitada)
            .ThenBy(r => r.HoraDesde)
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<Retiro>> ListarParaEntregaComercioAsync(
        Guid operadorId,
        CancellationToken ct)
    {
        IReadOnlyList<Retiro> resultado = retiros
            .Where(r =>
                r.OperadorId == operadorId &&
                r.Estado == EstadoRetiro.EnDeposito)
            .OrderBy(r => r.FechaSolicitada)
            .ThenBy(r => r.HoraDesde)
            .ToList();

        return Task.FromResult(resultado);
    }
}
