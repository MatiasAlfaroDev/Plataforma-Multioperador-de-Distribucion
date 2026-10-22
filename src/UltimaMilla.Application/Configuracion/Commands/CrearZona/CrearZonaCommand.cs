
namespace UltimaMilla.Application.Configuracion.Commands.CrearZona;

public sealed record CoberturaCommand(
    string Localidad,
    string? Sector);

public sealed record FranjaEntregaCommand(
    TimeOnly HoraDesde,
    TimeOnly HoraHasta);

public sealed record CrearZonaCommand(
    Guid OperadorId,
    string Nombre,
    string Departamento,
    int MinutosTraslado,
    IReadOnlyList<CoberturaCommand> Coberturas,
    IReadOnlyList<FranjaEntregaCommand> Franjas);
