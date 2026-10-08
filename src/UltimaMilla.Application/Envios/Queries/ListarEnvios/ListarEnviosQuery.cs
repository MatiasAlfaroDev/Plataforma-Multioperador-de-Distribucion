namespace UltimaMilla.Application.Envios.Queries.ListarEnvios;

/// <summary>
/// Lista los envíos visibles para el tenant actual.
/// El operador se obtiene del contexto autenticado y no desde un parámetro enviado por el cliente.
/// </summary>
public sealed record ListarEnviosQuery;