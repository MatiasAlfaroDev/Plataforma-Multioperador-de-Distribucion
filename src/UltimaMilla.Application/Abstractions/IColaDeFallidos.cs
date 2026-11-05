namespace UltimaMilla.Application.Abstractions;

/// <summary>
/// Un mensaje que agotó sus reintentos: el tipo, el motivo del último error y el cuerpo tal como está en la cola.
/// </summary>
public sealed record MensajeFallidoDto(string? TipoMensaje, string? Error, string Cuerpo);

/// <summary>
/// Una cola de fallidos (*_error) con su cantidad y una muestra de los primeros mensajes.
/// </summary>
public sealed record ColaFallidosDto(string Cola, int Cantidad, IReadOnlyList<MensajeFallidoDto> Muestra);

/// <summary>
/// Colas de mensajes fallidos del broker (RNF 6.8: el administrador las consulta y las reintenta).
/// </summary>
public interface IColaDeFallidos
{
    /// <summary>
    /// Las colas *_error con mensajes, cada una con hasta <paramref name="muestra"/> mensajes sin sacarlos de la cola.
    /// </summary>
    Task<IReadOnlyList<ColaFallidosDto>> ListarAsync(int muestra, CancellationToken ct);

    /// <summary>
    /// Devuelve todos los mensajes de la cola de fallidos a su cola original; devuelve cuántos movió.
    /// </summary>
    Task<int> ReintentarAsync(string colaDeFallidos, CancellationToken ct);
}
