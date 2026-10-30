namespace UltimaMilla.Application.Rutas.Commands.DespacharMiHojaDeRuta;

/// <summary>
/// El repartidor terminó de cargar el vehículo y sale (RF 17 y 19): manda los códigos de bultos
/// que escaneó. El servidor vuelve a verificar contra la hoja antes de despachar.
/// </summary>
public sealed record DespacharMiHojaDeRutaCommand(
    Guid HojaDeRutaId,
    IReadOnlyList<string> BultosEscaneados);
