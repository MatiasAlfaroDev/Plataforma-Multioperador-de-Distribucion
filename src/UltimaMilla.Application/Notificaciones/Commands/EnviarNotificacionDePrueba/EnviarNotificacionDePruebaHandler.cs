using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Application.Notificaciones.Commands.EnviarNotificacionDePrueba;

public sealed class EnviarNotificacionDePruebaHandler(
    IDispositivoMovilRepository dispositivos,
    INotificadorPush notificador,
    ITenantContext sesion,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoNotificacionDePrueba> Handle(
        EnviarNotificacionDePruebaCommand command,
        CancellationToken ct)
    {
        var usuarioId = sesion.UsuarioId
            ?? throw new UnauthorizedAccessException("Hay que iniciar sesión.");

        var telefonos = await dispositivos.ListarPorUsuarioAsync(usuarioId, ct);

        if (telefonos.Count == 0)
            throw new NoEncontradoException(
                "No hay ningún teléfono registrado para este usuario. Iniciá sesión en la app primero.");

        var notificacion = new NotificacionPush(
            string.IsNullOrWhiteSpace(command.Titulo) ? "Aviso de prueba" : command.Titulo.Trim(),
            string.IsNullOrWhiteSpace(command.Cuerpo) ? "Las notificaciones push funcionan." : command.Cuerpo.Trim(),
            new Dictionary<string, string> { ["tipo"] = "prueba" });

        var enviadas = 0;
        var eliminados = 0;

        foreach (var telefono in telefonos)
        {
            var resultado = await notificador.EnviarAsync(telefono.Token, notificacion, ct);

            switch (resultado)
            {
                case ResultadoPush.Enviado:
                    enviadas++;
                    break;

                // Ciclo de vida: un token que Firebase ya no reconoce se borra, así no se le sigue mandando.
                case ResultadoPush.TokenInvalido:
                    dispositivos.Eliminar(telefono);
                    eliminados++;
                    break;

                case ResultadoPush.NoConfigurado:
                    throw new InvalidOperationException(
                        "El servidor no tiene configurado Firebase: falta el archivo de la cuenta de servicio.");
            }
        }

        if (eliminados > 0)
            await unitOfWork.GuardarCambiosAsync(ct);

        return new ResultadoNotificacionDePrueba(telefonos.Count, enviadas, eliminados);
    }
}
