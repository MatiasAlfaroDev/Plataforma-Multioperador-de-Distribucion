namespace UltimaMilla.Application.CuentasComerciales.Gestion;

public sealed record ListarComerciosDelOperadorQuery;

public sealed record CambiarActividadComercioCommand(Guid CuentaId, bool Activa);

public sealed record ModificarDireccionComercioCommand(
    Guid CuentaId,
    string Calle,
    string Numero,
    string Localidad,
    string Departamento);
