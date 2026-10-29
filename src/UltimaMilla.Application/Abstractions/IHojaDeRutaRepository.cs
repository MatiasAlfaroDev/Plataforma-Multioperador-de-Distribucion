
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

    Task<IReadOnlyList<HojaDeRuta>> ListarAsync(
        Guid operadorId,
        CancellationToken ct);

    Task<HojaDeRuta?> ObtenerPorEnvioActivoAsync(
        Guid operadorId,
        Guid envioId,
        CancellationToken ct);
}
