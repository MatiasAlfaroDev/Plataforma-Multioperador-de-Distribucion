namespace UltimaMilla.Application.Envios.Queries.ConsultarDisponibilidadReprogramacionPublica;

public sealed record DisponibilidadReprogramacionPublicaDto(
    bool PuedeSolicitar,
    string? Motivo);