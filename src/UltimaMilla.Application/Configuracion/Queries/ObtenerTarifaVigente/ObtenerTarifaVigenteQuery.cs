namespace UltimaMilla.Application.Configuracion.Queries.ObtenerTarifaVigente;

public sealed record ObtenerTarifaVigenteQuery(
    Guid OperadorId,
    Guid ZonaCoberturaId);