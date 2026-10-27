using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// Hoja de ruta del repartidor, pensada para trabajar SIN conexión (RF 18, 21 y 24):
/// 1. Descargar: con conexión trae los envíos de la API y los guarda en SQLite cifrado.
/// 2. Trabajar sin señal: la lista se lee de SQLite; entregar o no entregar se guarda en el teléfono.
/// 3. Sincronizar: lo registrado se manda al servidor cuando vuelve la conexión (sola o con el botón).
/// </summary>
public partial class EnviosViewModel : ObservableObject, IQueryAttributable
{
    // Provisorio: en el diseño el catálogo de motivos lo define cada operador (VersionReglas).
    private static readonly string[] MotivosNoEntrega =
    [
        "Destinatario ausente",
        "Dirección incorrecta o inexistente",
        "Rechazado por el destinatario",
        "Zona inaccesible"
    ];

    private readonly IEnviosApi _api;
    private readonly RutaRepository _ruta;
    private readonly EventosPendientesRepository _eventos;
    private readonly SincronizacionService _sincronizacion;
    private readonly IConnectivity _conectividad;
    private readonly IDialogos _dialogos;

    public EnviosViewModel(
        IEnviosApi api,
        RutaRepository ruta,
        EventosPendientesRepository eventos,
        SincronizacionService sincronizacion,
        IConnectivity conectividad,
        IDialogos dialogos)
    {
        _api = api;
        _ruta = ruta;
        _eventos = eventos;
        _sincronizacion = sincronizacion;
        _conectividad = conectividad;
        _dialogos = dialogos;
    }

    public ObservableCollection<EnvioRuta> Envios { get; } = new();

    [ObservableProperty]
    private OperadorItem? operador;

    [ObservableProperty]
    private bool estaCargando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAviso))]
    private string? aviso;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnLinea))]
    private bool sinConexion;

    public bool EnLinea => !SinConexion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayPendientes), nameof(TextoPendientes))]
    private int pendientes;

    // Resumen del día para la cabecera de la pantalla.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progreso), nameof(TextoProgreso))]
    private int total;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progreso), nameof(TextoProgreso))]
    private int entregados;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progreso), nameof(TextoProgreso))]
    private int noEntregados;

    [ObservableProperty]
    private int porEntregar;

    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool HayAviso => !string.IsNullOrEmpty(Aviso);
    public bool HayPendientes => Pendientes > 0;
    public string TextoBotonSincronizar => "Enviar ahora";
    public string TextoPendientes => Pendientes == 1
        ? "1 registro guardado en el teléfono sin enviar"
        : $"{Pendientes} registros guardados en el teléfono sin enviar";

    /// <summary>De 0 a 1, para la barra de progreso: cuántos envíos ya tienen resultado.</summary>
    public double Progreso => Total == 0 ? 0 : (double)(Entregados + NoEntregados) / Total;
    public string TextoProgreso => $"{Entregados + NoEntregados} de {Total} resueltos";
    public string FechaHoy
    {
        get
        {
            var texto = DateTime.Now.ToString("dddd d 'de' MMMM", new System.Globalization.CultureInfo("es-UY"));
            return char.ToUpper(texto[0]) + texto[1..];   // "Miércoles 21 de octubre"
        }
    }

    /// <summary>Shell llama a este método con los parámetros de la navegación.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("operador", out var valor) && valor is OperadorItem elegido)
        {
            Operador = elegido;
            CargarCommand.Execute(null);
        }
    }

    /// <summary>La página avisa que aparece o desaparece: así el ViewModel escucha los eventos solo mientras se ve.</summary>
    public void OnAppearing()
    {
        _conectividad.ConnectivityChanged += AlCambiarConectividad;
        _sincronizacion.Sincronizado += AlSincronizar;
        SinConexion = _conectividad.NetworkAccess != NetworkAccess.Internet;
    }

    public void OnDisappearing()
    {
        _conectividad.ConnectivityChanged -= AlCambiarConectividad;
        _sincronizacion.Sincronizado -= AlSincronizar;
    }

    private void AlCambiarConectividad(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => SinConexion = e.NetworkAccess != NetworkAccess.Internet);

    private void AlSincronizar(object? sender, ResultadoSincronizacion r) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            Aviso = DescribirSincronizacion(r);
            await RefrescarDesdeBaseLocalAsync();
        });

    /// <summary>Descarga la ruta si hay conexión; si no, usa la que está guardada en el teléfono.</summary>
    [RelayCommand]
    private async Task CargarAsync()
    {
        if (Operador is null)
            return;

        EstaCargando = true;
        Error = null;
        try
        {
            SinConexion = _conectividad.NetworkAccess != NetworkAccess.Internet;
            if (!SinConexion)
            {
                try
                {
                    // Primero se intenta subir lo pendiente, así la ruta descargada ya lo refleja.
                    await _sincronizacion.SincronizarAsync();
                    var descargados = await _api.ListarEnviosAsync(Operador.Id);
                    await _ruta.GuardarRutaAsync(Operador.Id, descargados);
                    Aviso = $"Ruta descargada a las {DateTime.Now:HH:mm}.";
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    SinConexion = true;
                }
            }

            if (SinConexion)
                Aviso = "Sin conexión: trabajando con la ruta guardada en el teléfono.";

            await RefrescarDesdeBaseLocalAsync();
        }
        catch (Exception ex)
        {
            Error = $"Error en la base local: {ex.Message}";
        }
        finally
        {
            EstaCargando = false;
        }
    }

    [RelayCommand]
    private async Task EntregarAsync(EnvioRuta envio)
    {
        if (!envio.PuedeRegistrar)
            return;

        var receptor = await _dialogos.PedirTextoAsync("Entrega", $"{envio.ReferenciaExterna}: ¿quién recibe el paquete?");
        if (receptor is null)
            return;

        await RegistrarAsync(new EventoPendiente
        {
            Id = Guid.NewGuid(),
            EnvioId = envio.Id,
            Tipo = "Entregado",
            Receptor = receptor,
            FechaHora = DateTimeOffset.UtcNow
        });
    }

    [RelayCommand]
    private async Task NoEntregadoAsync(EnvioRuta envio)
    {
        if (!envio.PuedeRegistrar)
            return;

        var motivo = await _dialogos.ElegirAsync($"{envio.ReferenciaExterna}: motivo de no entrega", MotivosNoEntrega);
        if (motivo is null)
            return;

        await RegistrarAsync(new EventoPendiente
        {
            Id = Guid.NewGuid(),
            EnvioId = envio.Id,
            Tipo = "NoEntregado",
            Motivo = motivo,
            FechaHora = DateTimeOffset.UtcNow
        });
    }

    [RelayCommand]
    private async Task SincronizarAsync()
    {
        if (_conectividad.NetworkAccess != NetworkAccess.Internet)
        {
            Aviso = "Sin conexión: los datos quedan guardados y se envían solos cuando vuelva la señal.";
            return;
        }

        EstaCargando = true;
        try
        {
            var resultado = await _sincronizacion.SincronizarAsync();
            Aviso = DescribirSincronizacion(resultado);
            await RefrescarDesdeBaseLocalAsync();
        }
        finally
        {
            EstaCargando = false;
        }
    }

    /// <summary>El servidor rechazó lo registrado: el repartidor lo descarta y puede volver a registrarlo.</summary>
    [RelayCommand]
    private async Task DescartarAsync(EnvioRuta envio)
    {
        if (!envio.Rechazado)
            return;

        var opcion = await _dialogos.ElegirAsync(
            $"{envio.ReferenciaExterna}: {envio.UltimoError}",
            ["Descartar y volver a registrar"]);
        if (opcion is null)
            return;

        await _eventos.DescartarAsync(envio.Id);
        Aviso = $"{envio.ReferenciaExterna}: descartado. Quedó como \"Por entregar\".";
        await RefrescarDesdeBaseLocalAsync();
    }

    /// <summary>Siempre se guarda primero en el teléfono; si hay señal, se intenta mandar enseguida.</summary>
    private async Task RegistrarAsync(EventoPendiente evento)
    {
        try
        {
            await _eventos.RegistrarAsync(evento);
            await RefrescarDesdeBaseLocalAsync();

            if (_conectividad.NetworkAccess == NetworkAccess.Internet)
                _ = _sincronizacion.SincronizarAsync();
            else
                Aviso = "Guardado en el teléfono. Se enviará cuando vuelva la conexión.";
        }
        catch (Exception ex)
        {
            Error = $"No se pudo guardar: {ex.Message}";
        }
    }

    private async Task RefrescarDesdeBaseLocalAsync()
    {
        if (Operador is null)
            return;

        var lista = await _ruta.ListarRutaAsync(Operador.Id);
        Envios.Clear();
        foreach (var envio in lista)
            Envios.Add(envio);

        Total = lista.Count;
        Entregados = lista.Count(e => e.EsEntregado);
        NoEntregados = lista.Count(e => e.EsNoEntregado);
        PorEntregar = lista.Count(e => e.PuedeRegistrar);

        Pendientes = await _eventos.ContarAsync();
    }

    private static string DescribirSincronizacion(ResultadoSincronizacion r)
    {
        if (r.SinConexion)
            return $"Se cortó la conexión. Enviados: {r.Enviados}. Quedan {r.Pendientes} pendientes.";
        if (r.Rechazados > 0)
            return $"Enviados: {r.Enviados}. El servidor rechazó {r.Rechazados}; quedan guardados para revisar.";
        return r.Enviados > 0
            ? $"Sincronizado: {r.Enviados} evento(s) enviado(s) a las {DateTime.Now:HH:mm}."
            : "No había nada pendiente.";
    }
}
