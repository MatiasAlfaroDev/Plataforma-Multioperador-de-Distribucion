using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Application.Avisos.Commands.RegistrarAvisosRetiro;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Retiros.Commands.GestionarRetiro;

public sealed class GestionarRetiroHandler(
    IRetiroRepository retiros,
    IHojaDeRutaRepository hojas,
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

    /// <summary>RF 38: un retiro no retirado vuelve a Solicitado para asignarlo a otra hoja de ruta.</summary>
    public async Task ReprogramarAsync(
        Guid retiroId,
        CancellationToken ct)
    {
        var retiro = await ObtenerRetiroAsync(retiroId, ct);

        retiro.Reprogramar();

        await avisos.PrepararAsync(
            retiro.Id,
            TiposAviso.RetiroSolicitado,
            $"retiro:{retiro.Id}:reprogramado:{reloj.GetUtcNow():O}",
            reloj.GetUtcNow(),
            ct);

        await unitOfWork.GuardarCambiosAsync(ct);
    }

    public async Task EntregarAlComercioAsync(
        Guid retiroId,
        CancellationToken ct)
    {
        var retiro = await ObtenerRetiroAsync(retiroId, ct);

        var hojaActiva = await hojas.ObtenerPorRetiroEnEntregaActivoAsync(
            retiro.OperadorId,
            retiro.Id,
            ct);

        if (hojaActiva is not null &&
            hojaActiva.Estado != EstadoHojaDeRuta.Despachada)
        {
            throw new InvalidOperationException(
                "La hoja de ruta todavía no fue despachada.");
        }

        // La entidad Retiro valida la transición
        // EnDeposito -> EntregadoAlComercio.
        retiro.EntregarAlComercio();

        // Si se entregó mediante una hoja de ruta,
        // también se finaliza su parada.
        if (hojaActiva is not null)
        {
            hojaActiva.FinalizarParadaEntregaRetiroComercio(
                retiro,
                reloj.GetUtcNow());
        }

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