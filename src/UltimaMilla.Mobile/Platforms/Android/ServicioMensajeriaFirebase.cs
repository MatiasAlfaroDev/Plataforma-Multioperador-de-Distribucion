using Android.App;
using Firebase.Messaging;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile;

/// <summary>
/// Servicio de Android que Firebase despierta cuando llega un mensaje o cambia el token.
/// Lo declara el atributo [Service]: no hace falta tocar el AndroidManifest.xml para esto.
/// </summary>
[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class ServicioMensajeriaFirebase : FirebaseMessagingService
{
    /// <summary>
    /// Llega un aviso con la app ABIERTA. (Con la app cerrada Android lo muestra solo y este
    /// método no se llama.) Se muestra igual para que el repartidor no se lo pierda.
    /// </summary>
    public override void OnMessageReceived(RemoteMessage mensaje)
    {
        base.OnMessageReceived(mensaje);

        var aviso = mensaje.GetNotification();
        var titulo = aviso?.Title ?? "Última Milla";
        var cuerpo = aviso?.Body ?? string.Empty;

        CanalesDeAvisos.Mostrar(this, titulo, cuerpo);
    }

    /// <summary>
    /// Firebase cambió el token (primera vez, reinstalación, renovación). Si hay sesión abierta
    /// se manda enseguida; si no, se registra en el próximo inicio de sesión.
    /// </summary>
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);

        var registro = IPlatformApplication.Current?.Services.GetService<RegistroDePush>();
        if (registro is not null)
            _ = registro.RegistrarTokenAsync(token);
    }
}
