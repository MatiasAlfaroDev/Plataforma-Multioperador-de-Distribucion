namespace UltimaMilla.Application.Rutas.Commands.DespacharHojaDeRuta;

public sealed record DespacharHojaDeRutaCommand(
    Guid OperadorId,
    Guid HojaDeRutaId,
    // El usuario de la sesión; lo completa la API.
    string Responsable = "Despachador");