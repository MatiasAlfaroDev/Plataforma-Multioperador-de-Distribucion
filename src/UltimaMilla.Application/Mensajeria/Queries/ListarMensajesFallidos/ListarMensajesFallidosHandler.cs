using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Application.Mensajeria.Queries.ListarMensajesFallidos;

/// <summary>
/// Colas de fallidos con una muestra de sus mensajes (RNF 6.8). Las colas mezclan a todos los operadores:
/// el endpoint la restringe al administrador de plataforma.
/// </summary>
public sealed class ListarMensajesFallidosHandler(IColaDeFallidos colas)
{
    private const int Muestra = 10;

    public Task<IReadOnlyList<ColaFallidosDto>> Handle(ListarMensajesFallidosQuery query, CancellationToken ct) =>
        colas.ListarAsync(Muestra, ct);
}
