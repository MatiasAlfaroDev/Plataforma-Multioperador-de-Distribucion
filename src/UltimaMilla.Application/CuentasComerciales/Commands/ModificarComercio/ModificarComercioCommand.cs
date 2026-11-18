namespace UltimaMilla.Application.CuentasComerciales.Commands.ModificarComercio;

/// <summary>RF 1: el administrador del operador modifica un comercio aceptado, identificado por su cuenta.</summary>
public sealed record ModificarComercioCommand(Guid CuentaComercialId, string Nombre);
