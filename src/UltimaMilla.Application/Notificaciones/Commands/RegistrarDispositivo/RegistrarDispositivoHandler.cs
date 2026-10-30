using FluentValidation;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.Application.Notificaciones.Commands.RegistrarDispositivo;

/// <summary>
/// Guarda o renueva el token de Firebase del teléfono. Es idempotente: la app lo llama
/// en cada inicio de sesión y, si el token ya estaba, solo se actualiza la fecha y el usuario.
/// </summary>
public sealed class RegistrarDispositivoHandler(
    IDispositivoMovilRepository dispositivos,
    ITenantContext sesion,
    IUnitOfWork unitOfWork,
    IValidator<RegistrarDispositivoCommand> validator,
    TimeProvider reloj)
{
    public async Task Handle(RegistrarDispositivoCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException("Hay que iniciar sesión.");

        var operadorId = sesion.OperadorId
            ?? throw new InvalidOperationException("Primero hay que elegir un operador.");

        var ahora = reloj.GetUtcNow();
        var token = command.Token.Trim();

        var existente = await dispositivos.ObtenerPorTokenAsync(token, ct);

        if (existente is null)
        {
            await dispositivos.AgregarAsync(
                DispositivoMovil.Registrar(operadorId, usuarioId, token, command.Plataforma, ahora),
                ct);
        }
        else
        {
            existente.Renovar(usuarioId, ahora);
        }

        await unitOfWork.GuardarCambiosAsync(ct);
    }
}
