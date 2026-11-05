using UltimaMilla.Application.CuentasComerciales;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;

namespace UltimaMilla.Application.Abstractions;

public interface ICuentaComercialRepository
{
    Task<CuentaComercial?> ObtenerAsync(
        Guid id,
        CancellationToken ct);

    Task<IReadOnlyList<CuentaComercialDto>> ListarResumenAsync(
        Guid operadorId,
        Guid? cuentaComercialId,
        CancellationToken ct);

    // Buscar si un comercio ya tiene una vinculación con un operador.
    Task<CuentaComercial?> ObtenerPorOperadorYComercioAsync(
        Guid operadorId,
        Guid comercioId,
        CancellationToken ct);

    // Registrar una nueva solicitud de vinculación.
    Task AgregarAsync(
        CuentaComercial cuenta,
        CancellationToken ct);

    Task<IReadOnlyList<SolicitudPendienteDto>>
    ListarSolicitudesPendientesAsync(
        Guid operadorId,
        CancellationToken ct);
}