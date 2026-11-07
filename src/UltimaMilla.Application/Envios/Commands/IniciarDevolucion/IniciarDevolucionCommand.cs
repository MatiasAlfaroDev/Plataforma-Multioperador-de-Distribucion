
namespace UltimaMilla.Application.Envios.Commands.IniciarDevolucion;

public sealed record IniciarDevolucionCommand(
    Guid EnvioId,
    string Responsable);
