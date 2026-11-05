using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Eventos;

namespace UltimaMilla.Application.Avisos.Commands.ReintentarAvisosFallidos;

/// <summary>
/// Reintento programado de los avisos fallidos con espera creciente (RNF 6.9, escenario 6 de la defensa: el receptor vuelve y los avisos se recuperan solos).
/// </summary>
public sealed class ReintentarAvisosFallidosHandler(
    IEntregaAvisoRepository entregas,
    IPublicadorDeEventos eventos,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<int> Handle(ReintentarAvisosFallidosCommand command, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var fallidas = await entregas.ListarFallidasAsync(ct);

        var cantidad = 0;

        foreach (var e in fallidas)
        {
            if (e.PuedeReintentar(ahora))
            {
                e.Reencolar();
                // Antes de guardar: el outbox lo confirma en la misma transacción.
                await eventos.PublicarAsync(new AvisoPendiente(e.Id, e.OperadorId), ct);
                cantidad++;
            }
        }

        if (cantidad > 0)
            await unitOfWork.GuardarCambiosAsync(ct);

        return cantidad;
    }
}
