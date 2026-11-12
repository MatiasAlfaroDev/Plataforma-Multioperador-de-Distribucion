
namespace UltimaMilla.Application.CuentasComerciales.Commands.ConfigurarDireccionRecepcion;

public sealed record ConfigurarDireccionRecepcionCommand(
    Guid CuentaComercialId,
    string Calle,
    string Numero,
    string Localidad,
    string Departamento);
