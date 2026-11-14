using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;

namespace UltimaMilla.Application.Rutas.Commands.QuitarRetiroDeRuta;

public sealed record QuitarRetiroDeRutaCommand(Guid OperadorId, Guid HojaDeRutaId, Guid RetiroId);

/// <summary>Saca un retiro de una hoja en preparación: la parada se libera y el retiro vuelve a Solicitado.</summary>
public sealed class QuitarRetiroDeRutaHandler(
    IHojaDeRutaRepository hojas,
    IRetiroRepository retiros,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task Handle(QuitarRetiroDeRutaCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperadorId == Guid.Empty ||
            command.HojaDeRutaId == Guid.Empty ||
            command.RetiroId == Guid.Empty)
        {
            throw new ArgumentException("El operador, la hoja y el retiro son obligatorios.");
        }

        var hoja = await hojas.ObtenerAsync(command.OperadorId, command.HojaDeRutaId, ct)
            ?? throw new NoEncontradoException("La hoja de ruta no existe.");

        var retiro = await retiros.ObtenerAsync(command.RetiroId, ct);
        if (retiro is null || retiro.OperadorId != command.OperadorId)
            throw new NoEncontradoException("El retiro no existe.");

        await unitOfWork.EjecutarEnTransaccionAsync(
            async transaccionCt =>
            {
                hoja.QuitarRetiro(retiro, reloj.GetUtcNow());

                // Igual que al quitar un envío: órdenes temporales por el índice único y después consecutivos.
                hoja.PrepararRenumeracionParadas();
                await unitOfWork.GuardarCambiosAsync(transaccionCt);

                hoja.RenumerarParadasActivas();
                await unitOfWork.GuardarCambiosAsync(transaccionCt);
            },
            ct);
    }
}
