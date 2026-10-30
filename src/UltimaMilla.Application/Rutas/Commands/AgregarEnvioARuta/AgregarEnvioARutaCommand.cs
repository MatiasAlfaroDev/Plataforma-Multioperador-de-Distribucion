namespace UltimaMilla.Application.Rutas.Commands.AgregarEnvioARuta;

public sealed record AgregarEnvioARutaCommand(
    Guid OperadorId,
    Guid HojaDeRutaId,
    Guid EnvioId,
    TimeOnly HoraEntregaDesde,
    TimeOnly HoraEntregaHasta);