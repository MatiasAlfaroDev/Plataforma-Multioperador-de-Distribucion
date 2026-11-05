using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Rutas;

namespace UltimaMilla.Application.Rutas.Commands.RegistrarPosiciones;

/// <summary>
/// Guarda las posiciones que manda la app del repartidor logueado. Idempotente: las que ya
/// estaban (mismo Id) se ignoran, así reintentar un lote nunca duplica el recorrido.
/// </summary>
public sealed class RegistrarPosicionesHandler(
    IRepartidorRepository repartidores,
    IPosicionRepartidorRepository posiciones,
    ITenantContext sesion,
    IUnitOfWork unitOfWork,
    IValidator<RegistrarPosicionesCommand> validator,
    TimeProvider reloj)
{
    /// <returns>Cuántas posiciones nuevas se guardaron.</returns>
    public async Task<int> Handle(RegistrarPosicionesCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException("Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new InvalidOperationException("Primero hay que elegir un operador.");

        var repartidor = await repartidores.ObtenerPorUsuarioAsync(operadorId, usuarioId, ct)
            ?? throw new NoEncontradoException(
                "Tu usuario no está vinculado a ningún repartidor de este operador.");

        // Un mismo Id repetido dentro del lote cuenta una vez.
        var lote = command.Posiciones.DistinctBy(p => p.Id).ToList();
        var yaGuardadas = await posiciones.ExistentesAsync(lote.Select(p => p.Id).ToList(), ct);
        var ahora = reloj.GetUtcNow();

        var nuevas = lote
            .Where(p => !yaGuardadas.Contains(p.Id))
            .Select(p => PosicionRepartidor.Registrar(
                p.Id, operadorId, repartidor.Id, p.Latitud, p.Longitud, p.RegistradaEn, ahora))
            .ToList();

        if (nuevas.Count == 0)
            return 0;

        await posiciones.AgregarAsync(nuevas, ct);
        await unitOfWork.GuardarCambiosAsync(ct);
        return nuevas.Count;
    }
}
