namespace UltimaMilla.Application.CuentasComerciales.Commands.CambiarEstadoCuentaComercial;

/// <summary>RF 1: baja lógica (Activa = false) o reactivación de un comercio aceptado por el operador.</summary>
public sealed record CambiarEstadoCuentaComercialCommand(Guid CuentaComercialId, bool Activa);
