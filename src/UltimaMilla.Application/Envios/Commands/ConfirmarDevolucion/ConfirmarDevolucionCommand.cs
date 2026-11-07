
namespace UltimaMilla.Application.Envios.Commands.ConfirmarDevolucion;

public sealed record ConfirmarDevolucionCommand(
    Guid CuentaComercialId,
    Guid EnvioId,
    IReadOnlyList<string> IdentificacionesEscaneadas,
    string Responsable,
    string Modalidad,
    string Receptor,
    string ReferenciaConstancia);
