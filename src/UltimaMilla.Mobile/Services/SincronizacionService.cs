using UltimaMilla.Mobile.Data;

namespace UltimaMilla.Mobile.Services;

public sealed record ResultadoSincronizacion(int Enviados, int Rechazados, bool SinConexion, int Pendientes);

/// <summary>
/// Manda al servidor la cola de eventos registrados sin conexión (RF 24: sincronización diferida).
/// - Se dispara sola cuando el teléfono recupera Internet (evento ConnectivityChanged).
/// - También se puede disparar a mano con el botón "Sincronizar".
/// - Manda los eventos en el orden en que ocurrieron y se detiene si se vuelve a cortar la conexión.
/// - Un evento sale de la cola solo si el servidor confirma: si se corta a mitad de camino, nada se pierde.
/// </summary>
public sealed class SincronizacionService
{
    private readonly IEnviosApi _api;
    private readonly EventosPendientesRepository _eventos;
    private readonly IConnectivity _conectividad;
    private readonly SemaphoreSlim _enCurso = new(1, 1);

    /// <summary>Avisa a quien esté mirando (el ViewModel) que terminó una sincronización.</summary>
    public event EventHandler<ResultadoSincronizacion>? Sincronizado;

    public SincronizacionService(IEnviosApi api, EventosPendientesRepository eventos, IConnectivity conectividad)
    {
        _api = api;
        _eventos = eventos;
        _conectividad = conectividad;
        _conectividad.ConnectivityChanged += AlCambiarConectividad;
    }

    public bool HayInternet => _conectividad.NetworkAccess == NetworkAccess.Internet;

    private async void AlCambiarConectividad(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.Internet)
            return;
        try
        {
            await SincronizarAsync();
        }
        catch
        {
            // Un error acá no debe tirar la app: el próximo cambio de conectividad o el botón reintentan.
        }
    }

    public async Task<ResultadoSincronizacion> SincronizarAsync(CancellationToken ct = default)
    {
        // Si ya hay una sincronización corriendo, no se arranca otra (evita mandar dos veces lo mismo).
        if (!await _enCurso.WaitAsync(0, ct))
            return new ResultadoSincronizacion(0, 0, false, await _eventos.ContarAsync());

        var enviados = 0;
        var rechazados = 0;
        var sinConexion = false;
        try
        {
            foreach (var evento in await _eventos.ListarAsync())
            {
                var (resultado, detalle) = await _api.EnviarEventoAsync(evento, ct);

                if (resultado == ResultadoEnvio.SinConexion)
                {
                    sinConexion = true;
                    break;
                }

                if (resultado == ResultadoEnvio.Aceptado)
                {
                    await _eventos.ConfirmarAsync(evento);
                    enviados++;
                }
                else
                {
                    await _eventos.RegistrarFalloAsync(evento, detalle ?? "Rechazado por el servidor");
                    rechazados++;
                }
            }
        }
        finally
        {
            _enCurso.Release();
        }

        var resumen = new ResultadoSincronizacion(enviados, rechazados, sinConexion, await _eventos.ContarAsync());
        Sincronizado?.Invoke(this, resumen);
        return resumen;
    }
}
