
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Abstractions;

public interface IHojaDeRutaRepository
{
    Task AgregarAsync(
        HojaDeRuta hoja,
        CancellationToken ct);

    Task<HojaDeRuta?> ObtenerAsync(
        Guid operadorId,
        Guid hojaId,
        CancellationToken ct);

    /// <summary>Las hojas de ruta de un repartidor para una fecha, con sus paradas.</summary>
    Task<IReadOnlyList<HojaDeRuta>> ListarDelRepartidorAsync(
        Guid operadorId,
        Guid repartidorId,
        DateOnly fecha,
        CancellationToken ct);

    Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
        Guid operadorId,
        CancellationToken ct);

    Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
        Guid operadorId,
        Guid envioId,
        CancellationToken ct);
}
