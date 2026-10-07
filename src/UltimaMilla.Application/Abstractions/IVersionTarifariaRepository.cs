using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Contrato de acceso a las versiones tarifarias.
/// Lo implementa Infrastructure con EF Core.
/// </summary>
public interface IVersionTarifariaRepository
{
    Task<VersionTarifaria?> ObtenerVigenteAsync(
        Guid operadorId,
        string departamento,
        DateTimeOffset fecha,
        CancellationToken ct);
}