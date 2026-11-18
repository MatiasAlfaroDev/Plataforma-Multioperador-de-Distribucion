namespace UltimaMilla.Application.CuentasComerciales.Commands.ModificarComercio;

/// <summary>RF 1: el administrador del comercio modifica los datos de su propio comercio (el de la sesión).</summary>
public sealed record ModificarComercioCommand(string Nombre);
