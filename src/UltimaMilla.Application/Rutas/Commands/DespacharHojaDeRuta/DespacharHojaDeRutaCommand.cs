namespace UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;

public sealed record DespacharHojaDeRutaCommand(
    Guid OperadorId,
    Guid HojaDeRutaId);