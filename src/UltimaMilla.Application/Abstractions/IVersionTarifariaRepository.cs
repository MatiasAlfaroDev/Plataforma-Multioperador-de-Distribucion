using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Abstractions;

public interface IVersionTarifariaRepository
{
    Task<VersionTarifaria?> ObtenerVigenteAsync(
        Guid operadorId,
        string departamento,
        DateTimeOffset fecha,
        CancellationToken ct,
        TipoServicioTarifario tipoServicio = TipoServicioTarifario.Entrega);

    Task<VersionTarifaria?> ObtenerVigentePorZonaAsync(
        Guid operadorId,
        Guid zonaCoberturaId,
        DateTimeOffset fecha,
        CancellationToken ct,
        TipoServicioTarifario tipoServicio = TipoServicioTarifario.Entrega);

    Task AgregarAsync(
        VersionTarifaria version,
        CancellationToken ct);
}