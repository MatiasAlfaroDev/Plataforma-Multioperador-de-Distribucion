
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Operadores.Commands.ActualizarColorOperador;

public sealed class ActualizarColorOperadorHandler(
    IOperadorRepository operadores,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> Handle(
        Guid operadorId,
        string nuevoColor,
        CancellationToken ct)
    {
        var operador = await operadores.ObtenerParaActualizarAsync(
            operadorId, ct);

        if (operador is null)
        {
            return false;
        }

        operador.ActualizarColorPrimario(nuevoColor);

        await unitOfWork.GuardarCambiosAsync(ct);

        return true;
    }
}
