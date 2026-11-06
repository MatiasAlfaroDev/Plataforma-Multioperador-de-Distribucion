
namespace UltimaMilla.Application.Reportes.Queries.ObtenerMotivosNoEntrega;

public sealed record MotivosNoEntregaDto(
    DateOnly Desde,
    DateOnly Hasta,
    int TotalIncidencias,
    IReadOnlyList<MotivoNoEntregaResumenDto> Motivos);

public sealed record MotivoNoEntregaResumenDto(
    string Codigo,
    string Descripcion,
    int IntentosFallidos,
    int BultosNoEntregados,
    int TotalIncidencias);
