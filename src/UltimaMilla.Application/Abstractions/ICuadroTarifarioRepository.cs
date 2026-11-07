using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Abstractions;

public interface ICuadroTarifarioRepository
{
    Task<CuadroTarifario?> ObtenerAsync(
        Guid operadorId,
        Guid cuadroTarifarioId,
        CancellationToken ct);

    Task AgregarAsync(
        CuadroTarifario cuadro,
        CancellationToken ct);

    Task<bool> ExisteNombreAsync(
        Guid operadorId,
        string nombre,
        CancellationToken ct);
}