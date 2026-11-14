using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// Rendición al regresar al depósito (RF 22). Es la carga del vehículo al revés: el repartidor escanea
/// lo que trae de vuelta (los envíos no entregados, los bultos que quedaron de una entrega parcial y los
/// bultos retirados en domicilios) y la pantalla avisa en el momento si falta o sobra algo.
/// "Rendir" se habilita solo si coincide todo; el servidor lo vuelve a verificar y la hoja queda pendiente
/// de control: después el depósito controla lo recibido en el Backoffice y cierra la hoja (los envíos y
/// retiros pasan al depósito en ese momento).
/// Se hace en el depósito, con conexión: antes se sube lo que quedó guardado en el teléfono, porque el
/// servidor tiene que conocer el resultado de todas las paradas para saber qué tiene que volver.
/// </summary>
public partial class RendicionViewModel : ObservableObject
{
    private readonly IEnviosApi _api;
    private readonly IDialogos _dialogos;
    private readonly ILectorCodigos _lector;
    private readonly IBiometria _biometria;
    private readonly ISeguimiento _seguimiento;
    private readonly SincronizacionService _sincronizacion;
    private readonly EventosPendientesRepository _eventos;

    public RendicionViewModel(
        IEnviosApi api,
        IDialogos dialogos,
        ILectorCodigos lector,
        IBiometria biometria,
        ISeguimiento seguimiento,
        SincronizacionService sincronizacion,
        EventosPendientesRepository eventos)
    {
        _api = api;
        _dialogos = dialogos;
        _lector = lector;
        _biometria = biometria;
        _seguimiento = seguimiento;
        _sincronizacion = sincronizacion;
        _eventos = eventos;
    }

    public bool HayLectorCamara => _lector.Disponible;

    /// <summary>Las hojas de hoy que todavía no se rindieron.</summary>
    public ObservableCollection<HojaDeRutaItem> Hojas { get; } = [];

    /// <summary>Lo que tiene que volver al depósito, bulto por bulto.</summary>
    public ObservableCollection<BultoCarga> Bultos { get; } = [];

    /// <summary>Lo escaneado que no tiene que volver con esta hoja.</summary>
    public ObservableCollection<string> Sobrantes { get; } = [];

    /// <summary>Paradas que el servidor todavía no tiene resueltas: hay que registrarlas (y sincronizar) antes de rendir.</summary>
    public ObservableCollection<string> SinResultado { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayHojaElegida))]
    private HojaDeRutaItem? hojaElegida;

    [ObservableProperty]
    private string codigo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    private string? mensaje;

    [ObservableProperty]
    private bool mensajeEsAlerta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? error;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RendirCommand))]
    private bool estaCargando;

    public bool HayHojaElegida => HojaElegida is not null;
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool HaySobrantes => Sobrantes.Count > 0;
    public bool HaySinResultado => SinResultado.Count > 0;

    public int Escaneados => Bultos.Count(b => b.Cargado);
    public int Faltantes => Bultos.Count - Escaneados;
    public double Progreso => Bultos.Count == 0 ? 1 : (double)Escaneados / Bultos.Count;
    public string TextoProgreso => Bultos.Count == 0
        ? "No vuelve ningún bulto: se entregó todo"
        : $"{Escaneados} de {Bultos.Count} bultos devueltos";

    /// <summary>La línea arriba del botón: qué falta para poder rendir.</summary>
    public string TextoRendicion =>
        SinResultado.Count > 0 ? "Hay paradas sin resultado: registralas antes de rendir"
        : Sobrantes.Count > 0 ? "Hay bultos escaneados que no son de esta hoja"
        : Faltantes > 0 ? $"Faltan {Faltantes} bultos por escanear"
        : "Todo coincide: podés rendir";

    public bool PuedeRendir =>
        !EstaCargando &&
        HojaElegida is not null &&
        SinResultado.Count == 0 &&
        Faltantes == 0 &&
        Sobrantes.Count == 0;

    /// <summary>La página llama a esto al aparecer: sube lo pendiente y descarga las hojas de hoy.</summary>
    [RelayCommand]
    private async Task CargarAsync()
    {
        Error = null;
        EstaCargando = true;
        try
        {
            if (!_sincronizacion.HayInternet)
            {
                Error = "Sin conexión: la rendición se hace en el depósito, con el Wi-Fi, para que el servidor tenga todo lo que registraste.";
                return;
            }

            // Primero se sube lo que quedó guardado en el teléfono: el servidor tiene que saber qué se entregó.
            await _sincronizacion.SincronizarAsync();
            var pendientes = await _eventos.ContarAsync();
            if (pendientes > 0)
                Error = $"Quedan {pendientes} registros sin enviar (o rechazados). Resolvelos en la ruta antes de rendir.";

            var hojas = await _api.ListarMisHojasAsync();
            Hojas.Clear();
            foreach (var hoja in hojas.Where(h => !h.EsRendida))
                Hojas.Add(hoja);

            if (Hojas.Count == 0)
                Error ??= "No tenés hojas de ruta para rendir hoy.";

            HojaElegida = Hojas.Count == 1 ? Hojas[0] : null;
        }
        catch (RutaNoDisponibleException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Error = "Sin conexión: la rendición se hace en el depósito, con el Wi-Fi.";
        }
        finally
        {
            EstaCargando = false;
            ActualizarResumen();
        }
    }

    [RelayCommand]
    private void ElegirHoja(HojaDeRutaItem hoja) => HojaElegida = hoja;

    /// <summary>Al elegir una hoja se pide al servidor qué tiene que volver con ella.</summary>
    partial void OnHojaElegidaChanged(HojaDeRutaItem? value)
    {
        Bultos.Clear();
        Sobrantes.Clear();
        SinResultado.Clear();
        Mensaje = null;
        ActualizarResumen();

        if (value is not null)
            _ = CargarRendicionAsync(value);
    }

    private async Task CargarRendicionAsync(HojaDeRutaItem hoja)
    {
        EstaCargando = true;
        try
        {
            var rendicion = await _api.ObtenerRendicionAsync(hoja.Id);

            foreach (var retorno in rendicion.Retornos)
            {
                var detalle = retorno.Tipo == "Retiro" ? "retirado en domicilio" : "no entregado";
                foreach (var bulto in retorno.Bultos)
                    Bultos.Add(new BultoCarga(Normalizar(bulto), retorno.Referencia, detalle, esRendicion: true));
            }

            foreach (var parada in rendicion.ParadasSinResultado)
                SinResultado.Add(parada);
        }
        catch (RutaNoDisponibleException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Error = "Sin conexión: no se pudo pedir la rendición al servidor.";
        }
        finally
        {
            EstaCargando = false;
            ActualizarResumen();
        }
    }

    [RelayCommand]
    private Task EscanearAsync() => LeerCodigoAsync(desdeGaleria: false);

    [RelayCommand]
    private Task EscanearDesdeImagenAsync() => LeerCodigoAsync(desdeGaleria: true);

    private async Task LeerCodigoAsync(bool desdeGaleria)
    {
        if (HojaElegida is null)
            return;

        var leido = await _lector.LeerAsync(desdeGaleria);
        if (leido is null)
        {
            MostrarMensaje("No se encontró ningún código en la imagen. Probá de nuevo, más cerca y con buena luz.", alerta: true);
            return;
        }

        Codigo = leido;
        AgregarCodigo();
    }

    /// <summary>Un código leído: devuelto, repetido o no corresponde a esta hoja.</summary>
    [RelayCommand]
    private void AgregarCodigo()
    {
        var leido = Normalizar(Codigo);
        Codigo = string.Empty;

        if (leido.Length == 0 || HojaElegida is null)
            return;

        var bulto = Bultos.FirstOrDefault(b => b.Codigo == leido);

        if (bulto is null)
        {
            if (!Sobrantes.Contains(leido))
                Sobrantes.Add(leido);

            MostrarMensaje($"{leido} no tiene que volver con esta hoja.", alerta: true);
        }
        else if (bulto.Cargado)
        {
            MostrarMensaje($"{leido} ya estaba escaneado.", alerta: false);
        }
        else
        {
            bulto.Cargado = true;
            MostrarMensaje($"{leido} devuelto ({bulto.Referencia}).", alerta: false);
        }

        ActualizarResumen();
    }

    [RelayCommand]
    private void QuitarSobrante(string codigoSobrante)
    {
        Sobrantes.Remove(codigoSobrante);
        MostrarMensaje($"{codigoSobrante} quitado.", alerta: false);
        ActualizarResumen();
    }

    [RelayCommand(CanExecute = nameof(PuedeRendir))]
    private async Task RendirAsync()
    {
        if (HojaElegida is not { } hoja)
            return;

        // Operación sensible (6.11): rendir termina la jornada de la hoja y entrega lo devuelto al depósito.
        var confirmacion = await _biometria.ConfirmarAsync("Vas a rendir la hoja de ruta en el depósito.");
        if (confirmacion == ResultadoBiometria.NoConfigurada)
        {
            await _dialogos.AvisarAsync("Rendir",
                "Para rendir, el teléfono necesita bloqueo de pantalla o huella configurados.");
            return;
        }
        if (confirmacion != ResultadoBiometria.Confirmado)
            return;

        EstaCargando = true;
        try
        {
            var escaneados = Bultos.Where(b => b.Cargado).Select(b => b.Codigo).ToList();
            var (ok, texto) = await _api.RendirAsync(hoja.Id, escaneados);

            if (!ok)
            {
                await _dialogos.AvisarAsync("No se pudo rendir", texto);
                return;
            }

            // Terminó la jornada de esta hoja: se deja de compartir la ubicación (RF 23).
            _seguimiento.Detener();
            Preferences.Remove(CargaVehiculoViewModel.ClaveCargaConfirmada(hoja.Id));

            await _dialogos.AvisarAsync("Rendición enviada", texto);
            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["recargar"] = true });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await _dialogos.AvisarAsync("Sin conexión", "No se pudo rendir. Probá de nuevo con el Wi-Fi del depósito.");
        }
        finally
        {
            EstaCargando = false;
            ActualizarResumen();
        }
    }

    private void MostrarMensaje(string texto, bool alerta)
    {
        Mensaje = texto;
        MensajeEsAlerta = alerta;
    }

    private void ActualizarResumen()
    {
        OnPropertyChanged(nameof(Escaneados));
        OnPropertyChanged(nameof(Faltantes));
        OnPropertyChanged(nameof(Progreso));
        OnPropertyChanged(nameof(TextoProgreso));
        OnPropertyChanged(nameof(HaySobrantes));
        OnPropertyChanged(nameof(HaySinResultado));
        OnPropertyChanged(nameof(PuedeRendir));
        OnPropertyChanged(nameof(TextoRendicion));
        RendirCommand.NotifyCanExecuteChanged();
    }

    private static string Normalizar(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}
