namespace UltimaMilla.Application.Configuracion.Queries.ObtenerReglasOperativasVigentes;

public sealed record ReglasOperativasVigentesDto(
    Guid VersionReglasOperativasId,
    int MaximoIntentosEntrega,
    int HorasMinimasEntreIntentos,
    bool RequierePruebaEntrega,
    int DiasMaximosParaDevolucion,
    DateTimeOffset VigenciaDesde);