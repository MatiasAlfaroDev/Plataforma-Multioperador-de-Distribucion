using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Eventos;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Avisos.Commands.ReenviarAviso;

/// <summary>
/// El comercio o el operador reenvían un aviso fallido (RNF 6.8 y 6.9).
/// </summary>
public sealed class ReenviarAvisoHandler(
    IEntregaAvisoRepository entregas,
    IPublicadorDeEventos eventos,
    IUnitOfWork unitOfWork,
    ITenantContext sesion)
{
    public async Task Handle(ReenviarAvisoCommand command, CancellationToken ct)
    {
        var (_, cuentaComercialId) = AlcanceAvisos.Resolver(sesion);

        var entrega = await entregas.ObtenerAsync(command.EntregaAvisoId, ct);

        // Un comercio no ve los avisos de otra cuenta: para él no existen.
        if (entrega is null || (cuentaComercialId.HasValue && entrega.CuentaComercialId != cuentaComercialId))
            throw new NoEncontradoException("El aviso no existe.");

        entrega.Reencolar();
        await eventos.PublicarAsync(new AvisoPendiente(entrega.Id, entrega.OperadorId), ct);
        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
