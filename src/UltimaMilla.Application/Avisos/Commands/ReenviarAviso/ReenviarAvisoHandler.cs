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
    ICuentaComercialRepository cuentas,
    IPublicadorDeEventos eventos,
    IUnitOfWork unitOfWork,
    ITenantContext sesion,
    IOperadorActivo operadorActivo)
{
    public async Task Handle(ReenviarAvisoCommand command, CancellationToken ct)
    {
        var alcance = await AlcanceAvisos.ResolverAsync(sesion, cuentas, ct);

        // Un comercio no ve los avisos de otra cuenta: para él no existen.
        var entrega = await entregas.ObtenerAsync(command.EntregaAvisoId, alcance.Cuentas?.Keys.ToList(), ct)
            ?? throw new NoEncontradoException("El aviso no existe.");

        // La sesión del comercio no tiene operador: se fija el de la cuenta para poder guardar.
        if (alcance.Cuentas is not null)
            operadorActivo.SeleccionarOperador(entrega.OperadorId);

        entrega.Reencolar();
        await eventos.PublicarAsync(new AvisoPendiente(entrega.Id, entrega.OperadorId), ct);
        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
