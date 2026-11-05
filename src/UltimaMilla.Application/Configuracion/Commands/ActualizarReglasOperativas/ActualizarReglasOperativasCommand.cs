namespace UltimaMilla.Application.Configuracion.Commands.ActualizarReglasOperativas;
using UltimaMilla.Domain.Configuracion;
public sealed record ActualizarReglasOperativasCommand(
    Guid OperadorId,
    int MaximoIntentosEntrega,
    int HorasMinimasEntreIntentos,
    bool RequierePruebaEntrega,
    int DiasMaximosParaDevolucion,
    IReadOnlyList<MotivoNoEntregaItem>? Motivos = null,
    bool AdmiteEntregaParcial = false,
    AccionBultosPendientes AccionBultosPendientes =
        AccionBultosPendientes.NuevaVisita,
    bool NuevaVisitaConRecargo = false);

public sealed record MotivoNoEntregaItem(
    string Codigo,
    string Descripcion,
    bool RequiereDevolucion);
