using System.Diagnostics;

namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Une las dos partes: le pide el token a Firebase (IServicioPush) y se lo manda a la API (IDispositivosApi).
/// Nunca lanza: si el push falla, el repartidor igual puede trabajar.
/// </summary>
public sealed class RegistroDePush(IServicioPush push, IDispositivosApi api)
{
    /// <summary>Al iniciar sesión: pide permiso, obtiene el token y lo registra.</summary>
    public async Task RegistrarAsync(CancellationToken ct = default)
    {
        try
        {
            // Sin permiso el aviso no se ve, pero igual se registra: si después lo da en Ajustes, ya funciona.
            await push.PedirPermisoAsync();

            var token = await push.ObtenerTokenAsync();
            if (token is null)
                return;

            await api.RegistrarAsync(token, push.Plataforma, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Push: no se pudo registrar el teléfono: {ex.Message}");
        }
    }

    /// <summary>Cuando Firebase cambia el token con la app abierta (ServicioMensajeriaFirebase.OnNewToken).</summary>
    public async Task RegistrarTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            await api.RegistrarAsync(token, push.Plataforma, ct);
        }
        catch (Exception ex)
        {
            // Sin sesión da 401: no pasa nada, se registra en el próximo inicio de sesión.
            Debug.WriteLine($"Push: no se pudo registrar el token nuevo: {ex.Message}");
        }
    }
}
