using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Queries.ListarAvisos;

/// <summary>
/// Consulta para listar avisos.
/// </summary>
public sealed record ListarAvisosQuery(EstadoEntregaAviso? Estado);
