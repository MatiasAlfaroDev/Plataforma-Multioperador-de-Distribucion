namespace UltimaMilla.Application.Configuracion.Queries.ObtenerReglasOperativasVigentes;

public sealed record ReglasOperativasVigentesDto(
    Guid VersionReglasOperativasId,
    int MaximoIntentosEntrega,
    int HorasMinimasEntreIntentos,
    bool RequierePruebaEntrega,
    int DiasMaximosParaDevolucion,
    DateTimeOffset VigenciaDesde,
    IReadOnlyList<MotivoNoEntregaDto> Motivos);

public sealed record MotivoNoEntregaDto(
    string Codigo,
    string Descripcion,
    bool RequiereDevolucion);