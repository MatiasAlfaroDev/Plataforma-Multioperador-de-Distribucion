namespace UltimaMilla.Application.Rutas.Commands.QuitarEnvioDeRuta;

public sealed record QuitarEnvioDeRutaCommand(
    Guid OperadorId,
    Guid HojaDeRutaId,
    Guid EnvioId,
    string Responsable);