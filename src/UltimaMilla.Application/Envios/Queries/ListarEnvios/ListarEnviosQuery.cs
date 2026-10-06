namespace UltimaMilla.Application.Envios.Queries.ListarEnvios;

/// <summary>
/// Lista envíos. OperadorId opcional filtra por operador.
/// Desde el 15/10 el operador saldrá del token del usuario y no de este parámetro.
/// </summary>
public sealed record ListarEnviosQuery(Guid? OperadorId);
