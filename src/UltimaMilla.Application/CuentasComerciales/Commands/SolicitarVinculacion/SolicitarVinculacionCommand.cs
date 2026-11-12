
namespace UltimaMilla.Application.CuentasComerciales.Commands.SolicitarVinculacion;

public sealed record SolicitarVinculacionCommand(
    Guid OperadorId,
    DireccionRecepcionCommand DireccionRecepcion);

public sealed record DireccionRecepcionCommand(
    string Calle,
    string Numero,
    string Localidad,
    string Departamento);
