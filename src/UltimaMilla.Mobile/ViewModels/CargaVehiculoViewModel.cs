using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// Carga del vehículo en el depósito (RF 19) y despacho de la ruta (RF 17).
/// El repartidor elige su hoja de hoy, escanea cada bulto y la pantalla avisa en el momento
/// si falta o sobra alguno. "Despachar" se habilita solo con la carga completa.
/// </summary>
public partial class CargaVehiculoViewModel : ObservableObject
{
    private readonly IEnviosApi _api;
    private readonly IDialogos _dialogos;

    public CargaVehiculoViewModel(IEnviosApi api, IDialogos dialogos)
    {
        _api = api;
        _dialogos = dialogos;
    }

    /// <summary>Las hojas de ruta de hoy del repartidor.</summary>
    public ObservableCollection<HojaDeRutaItem> Hojas { get; } = [];

    /// <summary>Los bultos de la hoja elegida, cada uno marcado como cargado o no.</summary>
    public ObservableCollection<BultoCarga> Bultos { get; } = [];

    /// <summary>Lo escaneado que NO es de esta hoja: hay que bajarlo del vehículo.</summary>
    public ObservableCollection<string> Sobrantes { get; } = [];

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
    [NotifyCanExecuteChangedFor(nameof(DespacharCommand))]
    private bool estaCargando;

    public bool HayHojaElegida => HojaElegida is not null;
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool HaySobrantes => Sobrantes.Count > 0;

    public int Cargados => Bultos.Count(b => b.Cargado);
    public int Faltantes => Bultos.Count - Cargados;
    public double Progreso => Bultos.Count == 0 ? 0 : (double)Cargados / Bultos.Count;
    public string TextoProgreso => $"{Cargados} de {Bultos.Count} bultos cargados";

    /// <summary>La línea arriba del botón: qué falta para poder salir.</summary>
    public string TextoDespacho =>
        HojaElegida is { YaDespachada: true } ? "Esta hoja ya fue despachada"
        : Sobrantes.Count > 0 ? "Bajá del vehículo los bultos que sobran"
        : Faltantes > 0 ? $"Faltan {Faltantes} bultos por cargar"
        : "Carga completa: podés salir";

    /// <summary>Se puede salir solo con todo cargado, nada de más y una hoja que todavía no salió.</summary>
    public bool PuedeDespachar =>
        !EstaCargando &&
        HojaElegida is { YaDespachada: false } &&
        Bultos.Count > 0 &&
        Faltantes == 0 &&
        Sobrantes.Count == 0;

    /// <summary>La página llama a esto al aparecer: descarga las hojas de hoy.</summary>
    [RelayCommand]
    private async Task CargarAsync()
    {
        Error = null;
        EstaCargando = true;
        try
        {
            var hojas = await _api.ListarMisHojasAsync();
            Hojas.Clear();
            foreach (var hoja in hojas)
                Hojas.Add(hoja);

            if (Hojas.Count == 0)
                Error = "No tenés hojas de ruta para hoy.";

            // Si hay una sola pendiente de salir, se elige sola.
            var pendientes = Hojas.Where(h => !h.YaDespachada).ToList();
            HojaElegida = pendientes.Count == 1 ? pendientes[0] : null;
        }
        catch (RutaNoDisponibleException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // La carga se hace en el depósito, con Wi-Fi: sin conexión no se puede verificar contra el servidor.
            Error = "Sin conexión: la carga del vehículo necesita conexión para verificar contra el servidor.";
        }
        finally
        {
            EstaCargando = false;
            ActualizarResumen();
        }
    }

    [RelayCommand]
    private void ElegirHoja(HojaDeRutaItem hoja) => HojaElegida = hoja;

    /// <summary>Al elegir otra hoja se arranca la carga de cero con sus bultos.</summary>
    partial void OnHojaElegidaChanged(HojaDeRutaItem? value)
    {
        Bultos.Clear();
        Sobrantes.Clear();
        Mensaje = null;

        if (value is not null)
        {
            foreach (var parada in value.Paradas.OrderBy(p => p.Orden))
                foreach (var bulto in parada.Bultos)
                    Bultos.Add(new BultoCarga(Normalizar(bulto), parada.ReferenciaExterna, parada.Destinatario));

            if (value.YaDespachada)
                Mensaje = "Esta hoja ya salió del depósito.";
        }

        ActualizarResumen();
    }

    /// <summary>
    /// Un código leído (con la cámara o escrito a mano). Cada lectura da una respuesta inmediata,
    /// como la pistola de un depósito: cargado, repetido o no es de esta ruta.
    /// </summary>
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
            // RF 19: aviso de sobrante.
            if (!Sobrantes.Contains(leido))
                Sobrantes.Add(leido);

            MostrarMensaje($"⚠ {leido} no es de esta ruta: bajalo del vehículo.", alerta: true);
        }
        else if (bulto.Cargado)
        {
            MostrarMensaje($"{leido} ya estaba cargado.", alerta: false);
        }
        else
        {
            bulto.Cargado = true;
            MostrarMensaje($"✓ {leido} cargado ({bulto.Referencia}).", alerta: false);
        }

        ActualizarResumen();
    }

    /// <summary>El repartidor bajó del vehículo un bulto que no era de su ruta.</summary>
    [RelayCommand]
    private void QuitarSobrante(string codigoSobrante)
    {
        Sobrantes.Remove(codigoSobrante);
        MostrarMensaje($"{codigoSobrante} quitado de la carga.", alerta: false);
        ActualizarResumen();
    }

    [RelayCommand(CanExecute = nameof(PuedeDespachar))]
    private async Task DespacharAsync()
    {
        if (HojaElegida is not { } hoja)
            return;

        EstaCargando = true;
        try
        {
            var escaneados = Bultos.Where(b => b.Cargado).Select(b => b.Codigo).ToList();
            var (ok, texto) = await _api.DespacharAsync(hoja.Id, escaneados);

            if (!ok)
            {
                // El servidor vuelve a verificar: si algo no coincide, dice qué falta o sobra.
                await _dialogos.AvisarAsync("No se pudo despachar", texto);
                return;
            }

            await _dialogos.AvisarAsync("¡En camino!", texto);

            // Vuelve a la ruta y la recarga: los envíos ahora están En tránsito y se pueden entregar.
            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["recargar"] = true });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await _dialogos.AvisarAsync("Sin conexión", "No se pudo despachar. Probá de nuevo cuando haya señal.");
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

    /// <summary>Recalcula contadores y el botón Despachar después de cada cambio.</summary>
    private void ActualizarResumen()
    {
        OnPropertyChanged(nameof(Cargados));
        OnPropertyChanged(nameof(Faltantes));
        OnPropertyChanged(nameof(Progreso));
        OnPropertyChanged(nameof(TextoProgreso));
        OnPropertyChanged(nameof(HaySobrantes));
        OnPropertyChanged(nameof(PuedeDespachar));
        OnPropertyChanged(nameof(TextoDespacho));
        DespacharCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Igual que el servidor: sin espacios y en mayúsculas ("ped-300-1 " es "PED-300-1").</summary>
    private static string Normalizar(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>Un bulto de la hoja elegida, con su estado en la carga.</summary>
public partial class BultoCarga(string codigo, string referencia, string destinatario) : ObservableObject
{
    public string Codigo { get; } = codigo;
    public string Referencia { get; } = referencia;
    public string Destinatario { get; } = destinatario;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icono), nameof(TextoEstado))]
    private bool cargado;

    public string Icono => Cargado ? "✅" : "⏳";
    public string TextoEstado => Cargado ? "Cargado" : "Falta cargar";
}
