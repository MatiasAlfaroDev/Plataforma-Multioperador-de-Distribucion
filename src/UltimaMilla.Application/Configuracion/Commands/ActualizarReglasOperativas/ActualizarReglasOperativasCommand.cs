namespace UltimaMilla.Application.Configuracion.Commands.ActualizarReglasOperativas;

public sealed record ActualizarReglasOperativasCommand(
    Guid OperadorId,
    int MaximoIntentosEntrega,
    int HorasMinimasEntreIntentos,
    bool RequierePruebaEntrega,
    int DiasMaximosParaDevolucion,
    IReadOnlyList<MotivoNoEntregaItem>? Motivos = null);

public sealed record MotivoNoEntregaItem(
    string Codigo,
    string Descripcion,
    bool RequiereDevolucion);