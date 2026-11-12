
namespace UltimaMilla.Application.CuentasComerciales.Queries.ObtenerDireccionRecepcion;

public sealed record ObtenerDireccionRecepcionQuery(
    Guid CuentaComercialId);

public sealed record DireccionRecepcionDto(
    string Calle,
    string Numero,
    string Localidad,
    string Departamento);
