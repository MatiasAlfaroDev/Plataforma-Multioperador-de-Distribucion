using System.Text.RegularExpressions;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Mensajeria.Commands.ReintentarMensajesFallidos;

/// <summary>
/// Devuelve los mensajes de una cola de fallidos a su cola original para que el Worker los procese otra vez (RNF 6.8).
/// Reprocesar es seguro: los consumidores son idempotentes (inbox y clave única de cada aviso).
/// </summary>
public sealed partial class ReintentarMensajesFallidosHandler(IColaDeFallidos colas)
{
    public Task<int> Handle(ReintentarMensajesFallidosCommand command, CancellationToken ct)
    {
        // Solo colas de fallidos de MassTransit: el nombre llega de la URL y termina en la API del broker.
        if (!ColaDeFallidos().IsMatch(command.Cola))
            throw new ArgumentException("La cola indicada no es una cola de mensajes fallidos.");

        return colas.ReintentarAsync(command.Cola, ct);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*_error$")]
    private static partial Regex ColaDeFallidos();
}
