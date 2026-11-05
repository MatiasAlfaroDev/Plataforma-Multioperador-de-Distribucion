namespace UltimaMilla.Application.Envios.Commands.CrearEnvio;

public sealed record CrearEnvioResultado(
    Guid EnvioId,
    string TokenSeguimiento);