
namespace UltimaMilla.Application.Rutas.Commands.CrearHojaDeRuta;

public sealed record CrearHojaDeRutaCommand(
    Guid OperadorId,
    Guid RepartidorId,
    Guid VehiculoId,
    DateOnly Fecha,
    TimeOnly HoraSalida,
    TimeOnly HoraEntregaDesde,
    TimeOnly HoraEntregaHasta,
    int MaximoParadas,
    IReadOnlyList<Guid> EnviosIds);
