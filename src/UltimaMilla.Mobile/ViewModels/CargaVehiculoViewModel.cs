using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// Carga del vehículo en el depósito (RF 19). El despachador ya despachó la hoja desde el Backoffice
/// (RF 17) y por eso le aparece al repartidor. Acá el repartidor escanea cada bulto que sube y la pantalla
/// avisa en el momento si falta o sobra alguno. "Confirmar carga" se habilita solo con la carga completa:
/// el servidor la vuelve a verificar y empieza la jornada (se comparte la ubicación, RF 23).
/// </summary>
public partial class CargaVehiculoViewModel : ObservableObject
{
    private readonly IEnviosApi _api;
    private readonly IDialogos _dialogos;
    private readonly ILectorCodigos _lector;
    private readonly IBiometria _biometria;
    private readonly ISeguimiento _seguimiento;

    public CargaVehiculoViewModel(
        IEnviosApi api, IDialogos dialogos, ILectorCodigos lector, IBiometria biometria, ISeguimiento seguimiento)
    {
        _api = api;
        _dialogos = dialogos;
        _lector = lector;
        _biometria = biometria;
        _seguimiento = seguimiento;
    }

    /// <summary>En Windows no hay lector de cámara: se muestra solo el campo de texto.</summary>
    public bool HayLectorCamara => _lector.Disponible;

    /// <summary>Saca una foto del código del bulto y lo agrega como si se hubiera escrito.</summary>
    [RelayCommand]
    private Task EscanearAsync() => LeerCodigoAsync(desdeGaleria: false);

    /// <summary>Lee el código de una imagen guardada (sirve para probar en el emulador, que no ve etiquetas reales).</summary>
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

    /// <summary>Las hojas de ruta de hoy del repartidor.</summary>
    public ObservableCollection<HojaDeRutaItem> Hojas { get; } = [];

    /// <summary>Los bultos de la hoja elegida, cada uno marcado como cargado o no.</summary>
    public ObservableCollection<BultoCarga> Bultos { get; } = [];

    /// <summary>Las paradas de retiro de la hoja (RF 39): se muestran para saber que la ruta no sale vacía.</summary>
    public ObservableCollection<ParadaItem> Retiros { get; } = [];

    public bool HayRetiros => Retiros.Count > 0;

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
    [NotifyCanExecuteChangedFor(nameof(ConfirmarCargaCommand))]
    private bool estaCargando;

    public bool HayHojaElegida => HojaElegida is not null;
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool HaySobrantes => Sobrantes.Count > 0;

    public int Cargados => Bultos.Count(b => b.Cargado);
    public int Faltantes => Bultos.Count - Cargados;
    public double Progreso => Bultos.Count == 0 ? 0 : (double)Cargados / Bultos.Count;
    public string TextoProgreso => $"{Cargados} de {Bultos.Count} bultos cargados";

    /// <summary>
    /// Ya se confirmó la carga de todos los bultos de la hoja. Lo cargado se guarda en el teléfono por hoja:
    /// si el despachador suma un envío urgente después (letra 6.11), sus bultos aparecen como faltantes
    /// hasta que el repartidor los levanta del depósito y vuelve a confirmar.
    /// </summary>
    public bool CargaConfirmada =>
        HojaElegida is { } hoja &&
        Bultos.Count > 0 &&
        Bultos.All(b => b.Cargado) &&
        Sobrantes.Count == 0 &&
        LeerCargados(hoja.Id).SetEquals(Bultos.Select(b => b.Codigo));

    internal static string ClaveCargaConfirmada(Guid hojaId) => $"carga-confirmada-{hojaId}";

    private static HashSet<string> LeerCargados(Guid hojaId)
    {
        var guardado = Preferences.Get(ClaveCargaConfirmada(hojaId), null);
        if (string.IsNullOrEmpty(guardado))
            return [];

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(guardado) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>La línea arriba del botón: qué falta para poder salir.</summary>
    public string TextoCarga =>
        HojaElegida is { EsRendida: true } ? "Esta hoja ya se rindió en el depósito"
        : CargaConfirmada ? "Carga confirmada: ya podés salir a la ruta"
        : Sobrantes.Count > 0 ? "Bajá del vehículo los bultos que sobran"
        : HayHojaElegida && Bultos.Count == 0 ? "Solo hay retiros: no hay bultos para cargar"
        : Faltantes > 0 ? $"Faltan {Faltantes} bultos por cargar"
        : "Carga completa: confirmala para salir";

    /// <summary>Se confirma solo con todo cargado, nada de más y una hoja que sigue en curso.</summary>
    public bool PuedeConfirmarCarga =>
        !EstaCargando &&
        HojaElegida is { EsRendida: false } &&
        // Una hoja con solo retiros no tiene bultos para cargar en el depósito, pero igual se confirma para salir (RF 39).
        (Bultos.Count > 0 || HojaElegida.ParadasActivas.Count > 0) &&
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
            // Una hoja ya rendida terminó su jornada: no hay nada que cargar.
            var hojas = await _api.ListarMisHojasAsync();
            Hojas.Clear();
            foreach (var hoja in hojas.Where(h => !h.EsRendida))
                Hojas.Add(hoja);

            // RF 17: la hoja aparece recién cuando el despachador la despacha desde el Backoffice.
            if (Hojas.Count == 0)
                Error = "Todavía no tenés hojas de ruta despachadas para hoy. Avisale al despachador.";

            // Si hay una sola, se elige sola.
            HojaElegida = Hojas.Count == 1 ? Hojas[0] : null;
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
        Retiros.Clear();
        Mensaje = null;

        if (value is not null)
        {
            // RF 39: los bultos de un retiro se suben en el domicilio, no en el depósito.
            foreach (var parada in value.ParadasActivas.Where(p => p.TipoOperacion != "Retiro").OrderBy(p => p.Orden))
                foreach (var bulto in parada.Bultos)
                    Bultos.Add(new BultoCarga(Normalizar(bulto), parada.ReferenciaExterna, parada.Destinatario));

            foreach (var retiro in value.ParadasActivas.Where(p => p.TipoOperacion == "Retiro").OrderBy(p => p.Orden))
                Retiros.Add(retiro);

            // Lo que ya se cargó y confirmó antes sigue cargado; lo nuevo (un envío urgente) queda faltante.
            var cargados = LeerCargados(value.Id);
            foreach (var bulto in Bultos.Where(b => cargados.Contains(b.Codigo)))
                bulto.Cargado = true;

            if (value.EsRendida)
                Mensaje = "Esta hoja ya se rindió en el depósito.";
            else if (cargados.Count > 0 && Bultos.Any(b => !b.Cargado))
                MostrarMensaje("Se agregó un envío urgente a tu hoja: levantá del depósito los bultos que faltan.", alerta: true);
            else if (cargados.Count > 0)
                Mensaje = "Ya confirmaste la carga de esta hoja.";
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

            MostrarMensaje($"{leido} no es de esta ruta: bajalo del vehículo.", alerta: true);
        }
        else if (bulto.Cargado)
        {
            MostrarMensaje($"{leido} ya estaba cargado.", alerta: false);
        }
        else
        {
            bulto.Cargado = true;
            MostrarMensaje($"{leido} cargado ({bulto.Referencia}).", alerta: false);
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

    [RelayCommand(CanExecute = nameof(PuedeConfirmarCarga))]
    private async Task ConfirmarCargaAsync()
    {
        if (HojaElegida is not { } hoja)
            return;

        // Operación sensible (6.11): con la confirmación, el repartidor se hace cargo de los bultos que subió.
        var confirmacion = await _biometria.ConfirmarAsync("Confirmás que cargaste los bultos de la hoja y salís del depósito.");
        if (confirmacion == ResultadoBiometria.NoConfigurada)
        {
            await _dialogos.AvisarAsync("Confirmar carga",
                "Para confirmar la carga, el teléfono necesita bloqueo de pantalla o huella configurados.");
            return;
        }
        if (confirmacion != ResultadoBiometria.Confirmado)
            return;

        EstaCargando = true;
        try
        {
            var escaneados = Bultos.Where(b => b.Cargado).Select(b => b.Codigo).ToList();
            var verificacion = await _api.VerificarCargaAsync(hoja.Id, escaneados);

            if (verificacion.Motivo is { } motivo)
            {
                await _dialogos.AvisarAsync("No se pudo verificar la carga", motivo);
                return;
            }

            if (!verificacion.Completa)
            {
                // El servidor vuelve a comparar con la hoja actual: si el despachador la cambió, se nota acá.
                await _dialogos.AvisarAsync("La carga no coincide con la hoja", DescribirDiferencias(verificacion));
                return;
            }

            Preferences.Set(ClaveCargaConfirmada(hoja.Id),
                System.Text.Json.JsonSerializer.Serialize(escaneados));

            // Empieza la jornada: se comparte la ubicación hasta rendir o "Terminar jornada" (RF 23).
            var siguiendo = await _seguimiento.IniciarAsync();
            await _dialogos.AvisarAsync("¡En camino!", siguiendo
                ? "Carga verificada. Ya podés salir a la ruta."
                : "Carga verificada. No se pudo compartir tu ubicación: revisá el permiso de ubicación de la app.");

            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["recargar"] = true });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await _dialogos.AvisarAsync("Sin conexión", "No se pudo verificar la carga. Probá de nuevo con el Wi-Fi del depósito.");
        }
        finally
        {
            EstaCargando = false;
            ActualizarResumen();
        }
    }

    private static string DescribirDiferencias(VerificacionCarga v)
    {
        var partes = new List<string>();
        if (v.Faltantes.Count > 0)
            partes.Add($"Faltan: {string.Join(", ", v.Faltantes)}.");
        if (v.Sobrantes.Count > 0)
            partes.Add($"Sobran: {string.Join(", ", v.Sobrantes)}.");
        partes.Add("Volvé a descargar la hoja si el despachador la cambió.");
        return string.Join("\n", partes);
    }

    private void MostrarMensaje(string texto, bool alerta)
    {
        Mensaje = texto;
        MensajeEsAlerta = alerta;
    }

    /// <summary>Recalcula contadores y el botón Confirmar carga después de cada cambio.</summary>
    private void ActualizarResumen()
    {
        OnPropertyChanged(nameof(Cargados));
        OnPropertyChanged(nameof(Faltantes));
        OnPropertyChanged(nameof(Progreso));
        OnPropertyChanged(nameof(TextoProgreso));
        OnPropertyChanged(nameof(HaySobrantes));
        OnPropertyChanged(nameof(HayRetiros));
        OnPropertyChanged(nameof(CargaConfirmada));
        OnPropertyChanged(nameof(PuedeConfirmarCarga));
        OnPropertyChanged(nameof(TextoCarga));
        ConfirmarCargaCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Igual que el servidor: sin espacios y en mayúsculas ("ped-300-1 " es "PED-300-1").</summary>
    private static string Normalizar(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>Un bulto de la hoja elegida, con su estado en la carga (o en la rendición, al volver).</summary>
public partial class BultoCarga(string codigo, string referencia, string destinatario, bool esRendicion = false) : ObservableObject
{
    public bool EsRendicion { get; } = esRendicion;

    public string Codigo { get; } = codigo;
    public string Referencia { get; } = referencia;
    public string Destinatario { get; } = destinatario;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icono), nameof(ColorIcono), nameof(TextoEstado))]
    private bool cargado;

    public string Icono => Cargado ? Iconos.TildeCirculo : Iconos.Reloj;
    public Color ColorIcono => Paleta.De(Cargado ? "Exito" : "TextoSecundario");
    public string TextoEstado => EsRendicion
        ? (Cargado ? "Bajado en el depósito" : "Falta escanear")
        : (Cargado ? "Cargado" : "Falta cargar");
}
