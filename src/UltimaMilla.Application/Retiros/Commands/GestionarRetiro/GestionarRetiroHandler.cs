using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Domain.Avisos;

namespace UltimaMilla.Application.Retiros.Commands.GestionarRetiro;

public sealed class GestionarRetiroHandler(
    IRetiroRepository retiros,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    RegistrarAvisosRetiroHandler avisos,
    TimeProvider reloj)
{
    public async Task RecibirEnDepositoAsync(
        Guid retiroId,
        CancellationToken ct)
    {
        var retiro = await ObtenerRetiroAsync(retiroId, ct);

        retiro.RecibirEnDeposito();

        await avisos.PrepararAsync(
            retiro.Id,
            TiposAviso.RetiroEnDeposito,
            $"retiro:{retiro.Id}:en_deposito",
            reloj.GetUtcNow(),
            ct);

        await unitOfWork.GuardarCambiosAsync(ct);
    }

    public async Task EntregarAlComercioAsync(
        Guid retiroId,
        CancellationToken ct)
    {
        var retiro = await ObtenerRetiroAsync(retiroId, ct);

        retiro.EntregarAlComercio();

        await avisos.PrepararAsync(
            retiro.Id,
            TiposAviso.RetiroEntregadoAlComercio,
            $"retiro:{retiro.Id}:entregado_al_comercio",
            reloj.GetUtcNow(),
            ct);

        await unitOfWork.GuardarCambiosAsync(ct);
    }

    private async Task<UltimaMilla.Domain.Retiros.Retiro>
        ObtenerRetiroAsync(
            Guid retiroId,
            CancellationToken ct)
    {
        if (tenant.OperadorId is not Guid operadorId)
            throw new UnauthorizedAccessException(
                "No hay un operador autenticado.");

        var retiro = await retiros.ObtenerAsync(retiroId, ct)
            ?? throw new NoEncontradoException(
                "El retiro no existe.");

        if (retiro.OperadorId != operadorId)
            throw new NoEncontradoException(
                "El retiro no existe.");

        return retiro;
    }
}