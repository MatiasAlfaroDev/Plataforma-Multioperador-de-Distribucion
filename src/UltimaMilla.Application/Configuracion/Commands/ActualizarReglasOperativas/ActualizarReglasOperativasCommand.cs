
namespace UltimaMilla.Application.Configuracion.Commands.ActualizarReglasOperativas;

public sealed record ActualizarReglasOperativasCommand(
    Guid OperadorId,
    int MaximoIntentosEntrega,
    int HorasMinimasEntreIntentos,
    bool RequierePruebaEntrega,
    int DiasMaximosParaDevolucion);
