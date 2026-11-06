
namespace UltimaMilla.Application.Reportes.Queries.ObtenerResumenEnvios;

public sealed record ResumenEnviosDto(
    DateOnly Desde,
    DateOnly Hasta,
    int TotalEnvios,
    IReadOnlyList<EstadoEnvioResumenDto> PorEstado);

public sealed record EstadoEnvioResumenDto(
    string Estado,
    int Cantidad);
