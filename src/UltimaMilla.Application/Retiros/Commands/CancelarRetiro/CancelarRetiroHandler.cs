using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Application.Retiros.Commands.CancelarRetiro;

public sealed record CancelarRetiroCommand(Guid RetiroId, string Motivo);

/// <summary>
/// Cancela un retiro todavía solicitado. Lo puede hacer el despacho del operador o el comercio dueño
/// de la cuenta; este último no tiene operador en la sesión, así que se fija el de su cuenta.
/// </summary>
public sealed class CancelarRetiroHandler(
    IRetiroRepository retiros,
    ICuentaComercialRepository cuentas,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    IOperadorActivo operadorActivo,
    RegistrarAvisosRetiroHandler avisos,
    TimeProvider reloj)
{
    public async Task Handle(CancelarRetiroCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.RetiroId == Guid.Empty)
            throw new ArgumentException("El retiro es obligatorio.");

        var retiro = await ObtenerRetiroAsync(command.RetiroId, ct);

        retiro.Cancelar(command.Motivo);

        await avisos.PrepararAsync(
            retiro,
            TiposAviso.RetiroCancelado,
            $"retiro:{retiro.Id}:cancelado",
            reloj.GetUtcNow(),
            ct);

        await unitOfWork.GuardarCambiosAsync(ct);
    }

    private async Task<Retiro> ObtenerRetiroAsync(Guid retiroId, CancellationToken ct)
    {
        if (!tenant.EstaAutenticado)
            throw new UnauthorizedAccessException("Debe iniciar sesión.");

        if (Perfiles.Despacho.Contains(tenant.Perfil) && tenant.OperadorId is Guid operadorId)
        {
            var delOperador = await retiros.ObtenerAsync(retiroId, ct);

            return delOperador is not null && delOperador.OperadorId == operadorId
                ? delOperador
                : throw new NoEncontradoException("El retiro no existe.");
        }

        if (Perfiles.PersonalComercio.Contains(tenant.Perfil) && tenant.ComercioId is Guid comercioId)
        {
            var vinculaciones = (await cuentas.ListarPorComercioAsync(comercioId, ct))
                .Where(c => c.Activa && c.Estado == EstadoCuentaComercial.Confirmada);

            foreach (var cuenta in vinculaciones)
            {
                operadorActivo.SeleccionarOperador(cuenta.OperadorId);

                var retiro = await retiros.ObtenerAsync(retiroId, ct);
                if (retiro is not null && retiro.CuentaComercialId == cuenta.Id)
                    return retiro;
            }

            throw new NoEncontradoException("El retiro no existe.");
        }

        throw new UnauthorizedAccessException("El perfil no puede cancelar retiros.");
    }
}
