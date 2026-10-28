using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Notificaciones;

/// <summary>
/// Manda los avisos con Firebase Cloud Messaging (FCM). FCM es quien despierta al teléfono:
/// por eso la notificación llega aunque la app esté cerrada.
/// </summary>
/// <remarks>
/// La cuenta de servicio es un secreto: NO está en el repo. El compose la monta como archivo de solo
/// lectura y pasa su ruta en Firebase__CredencialesRuta. Sin ese archivo la API arranca igual y este
/// notificador responde NoConfigurado (así no se rompe el entorno de quien no lo tenga).
/// </remarks>
public sealed class FirebaseNotificadorPush(
    IConfiguration configuracion,
    ILogger<FirebaseNotificadorPush> logger) : INotificadorPush
{
    private const string NombreApp = "ultimamilla-push";

    /// <summary>Mismo id de canal que crea la app (Platforms/Android/CanalesDeAvisos.cs).</summary>
    private const string CanalAndroid = "avisos_urgentes";

    private static readonly Lock Candado = new();

    public async Task<ResultadoPush> EnviarAsync(string token, NotificacionPush notificacion, CancellationToken ct)
    {
        var app = ObtenerApp();
        if (app is null)
            return ResultadoPush.NoConfigurado;

        var mensaje = new Message
        {
            Token = token,
            Notification = new Notification
            {
                Title = notificacion.Titulo,
                Body = notificacion.Cuerpo
            },
            Data = notificacion.Datos,
            Android = new AndroidConfig
            {
                // Alta prioridad: Android la entrega enseguida aunque el teléfono esté en reposo.
                Priority = Priority.High,
                Notification = new AndroidNotification { ChannelId = CanalAndroid }
            }
        };

        try
        {
            await FirebaseMessaging.GetMessaging(app).SendAsync(mensaje, ct);
            return ResultadoPush.Enviado;
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            // Unregistered: la app se desinstaló o se borraron sus datos. InvalidArgument: el token no es válido.
            logger.LogInformation("Token de push dado de baja: {Motivo}", ex.MessagingErrorCode);
            return ResultadoPush.TokenInvalido;
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning(ex, "No se pudo enviar la notificación push: {Motivo}", ex.MessagingErrorCode);
            return ResultadoPush.Fallido;
        }
    }

    /// <summary>Crea la conexión con Firebase la primera vez que se usa y la reutiliza después.</summary>
    private FirebaseApp? ObtenerApp()
    {
        lock (Candado)
        {
            var existente = FirebaseApp.GetInstance(NombreApp);
            if (existente is not null)
                return existente;

            var ruta = configuracion["Firebase:CredencialesRuta"];
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
            {
                logger.LogWarning(
                    "Push desactivado: no se encontró la cuenta de servicio de Firebase en '{Ruta}'.", ruta);
                return null;
            }

            return FirebaseApp.Create(
                new AppOptions { Credential = GoogleCredential.FromFile(ruta) },
                NombreApp);
        }
    }
}
