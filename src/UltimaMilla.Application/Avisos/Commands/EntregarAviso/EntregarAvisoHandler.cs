using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.EntregarAviso;

/// <summary>
/// Hace un intento de entrega; si falla, registra el fallo y relanza para que el Worker reintente.
/// Si ya estaba entregado no hace nada (idempotente).
/// </summary>
public sealed class EntregarAvisoHandler(
    IEntregaAvisoRepository entregas,
    ISuscripcionAvisoRepository suscripciones,
    IClienteAvisos cliente,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(EntregarAvisoCommand command, CancellationToken ct)
    {
        var entrega = await entregas.ObtenerAsync(command.EntregaAvisoId, ct)
            ?? throw new NoEncontradoException("El aviso no existe.");

        if (entrega.Estado == EstadoEntregaAviso.Entregada)
            return;

        var suscripcion = await suscripciones.ObtenerAsync(entrega.SuscripcionId, ct)
            ?? throw new NoEncontradoException("La suscripción del aviso no existe.");

        try
        {
            await cliente.EnviarAsync(suscripcion.Url, suscripcion.Secreto, entrega.Id, entrega.TipoEvento, entrega.Cuerpo, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            entrega.RegistrarFallo(ex.Message, reloj.GetUtcNow());
            await unitOfWork.GuardarCambiosAsync(ct);
            throw;
        }

        entrega.RegistrarExito(reloj.GetUtcNow());
        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
