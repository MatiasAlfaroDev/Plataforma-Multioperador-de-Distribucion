using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// RF 32: en cada parada, el repartidor indica mediante escaneo qué bultos entrega (o retira, RF 40).
/// Cada bulto se marca escaneando su etiqueta o, si no se puede leer, escribiendo su código: tocarlo en
/// la lista no alcanza. Los que quedan sin escanear son los no entregados.
/// </summary>
public partial class SeleccionBultosViewModel : ObservableObject
{
    private readonly ILectorCodigos _lector;

    public SeleccionBultosViewModel(string referencia, IReadOnlyList<BultoItem> bultos, ILectorCodigos lector, bool esRetiro = false)
    {
        Referencia = referencia;
        _lector = lector;
        EsRetiro = esRetiro;
        foreach (var bulto in bultos)
            Bultos.Add(new BultoParada(bulto, esRetiro));
    }

    /// <summary>Retiro (RF 40): la misma pantalla sirve para escanear en el domicilio los bultos que se retiran.</summary>
    public bool EsRetiro { get; }

    public string Titulo => EsRetiro ? "Escaneá los bultos que retirás" : "¿Qué bultos se entregan?";
    public string Instruccion => EsRetiro
        ? "Escaneá o escribí el código de cada bulto que te entregan"
        : "Escaneá o escribí el código de cada bulto que entregás";
    public string TituloLista => EsRetiro
        ? "Bultos del retiro (se marcan al escanearlos)"
        : "Bultos del envío (se marcan al escanearlos)";

    public string Referencia { get; }

    public ObservableCollection<BultoParada> Bultos { get; } = [];

    /// <summary>En Windows no hay lector de cámara: se muestra solo el campo de texto.</summary>
    public bool HayLectorCamara => _lector.Disponible;

    [ObservableProperty]
    private string codigo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    private string? mensaje;

    [ObservableProperty]
    private bool mensajeEsAlerta;

    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);

    public int Entregados => Bultos.Count(b => b.Entregado);

    public string TextoResumen =>
        EsRetiro
            ? (Entregados == Bultos.Count ? $"Tenés los {Bultos.Count} bultos" : $"Escaneaste {Entregados} de {Bultos.Count}")
        : Entregados == 0 ? "Escaneá los bultos que entregás"
        : Entregados == Bultos.Count ? $"Se entregan los {Bultos.Count} bultos"
        : $"Se entregan {Entregados} de {Bultos.Count}: los demás vuelven al depósito";

    [RelayCommand]
    private Task EscanearAsync() => LeerCodigoAsync(desdeGaleria: false);

    [RelayCommand]
    private Task EscanearDesdeImagenAsync() => LeerCodigoAsync(desdeGaleria: true);

    private async Task LeerCodigoAsync(bool desdeGaleria)
    {
        var leido = await _lector.LeerAsync(desdeGaleria);
        if (leido is null)
        {
            MostrarMensaje("No se encontró ningún código en la imagen. Probá de nuevo.", alerta: true);
            return;
        }

        Codigo = leido;
        AgregarCodigo();
    }

    /// <summary>Un código leído o escrito: marca ese bulto como entregado.</summary>
    [RelayCommand]
    private void AgregarCodigo()
    {
        var leido = Normalizar(Codigo);
        Codigo = string.Empty;
        if (leido.Length == 0)
            return;

        var bulto = Bultos.FirstOrDefault(b => Normalizar(b.Codigo) == leido);
        if (bulto is null)
            MostrarMensaje(EsRetiro ? $"{leido} no es de este retiro." : $"{leido} no es de este envío.", alerta: true);
        else if (bulto.Entregado)
            MostrarMensaje($"{leido} ya estaba marcado.", alerta: false);
        else
        {
            bulto.Entregado = true;
            MostrarMensaje(EsRetiro ? $"✓ {leido} retirado." : $"✓ {leido} se entrega.", alerta: false);
        }

        ActualizarResumen();
    }

    /// <summary>
    /// Tocar un bulto en la lista no lo marca (RF 32 y 40): cada bulto se identifica con su código, escaneado o
    /// escrito. Si se escaneó uno por error, tocarlo lo desmarca.
    /// </summary>
    [RelayCommand]
    private void Alternar(BultoParada bulto)
    {
        if (bulto.Entregado)
        {
            bulto.Entregado = false;
            MostrarMensaje($"{bulto.Codigo} desmarcado.", alerta: false);
            ActualizarResumen();
            return;
        }

        MostrarMensaje("Cada bulto se marca escaneando su etiqueta (o escribiendo el código si no se puede leer).", alerta: true);
    }

    public SeleccionBultos Resultado() => new(
        Bultos.Where(b => b.Entregado).Select(b => b.Bulto).ToList(),
        Bultos.Where(b => !b.Entregado).Select(b => b.Bulto).ToList());

    private void MostrarMensaje(string texto, bool alerta)
    {
        Mensaje = texto;
        MensajeEsAlerta = alerta;
    }

    private void ActualizarResumen()
    {
        OnPropertyChanged(nameof(Entregados));
        OnPropertyChanged(nameof(TextoResumen));
    }

    private static string Normalizar(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>Un bulto en la pantalla de la parada: se entrega o no.</summary>
public partial class BultoParada(BultoItem bulto, bool esRetiro = false) : ObservableObject
{
    public BultoItem Bulto { get; } = bulto;
    public string Codigo => Bulto.Identificacion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icono), nameof(ColorIcono), nameof(TextoEstado))]
    private bool entregado;

    public string Icono => Entregado ? Iconos.TildeCirculo : Iconos.Caja;
    public Color ColorIcono => Paleta.De(Entregado ? "Exito" : "TextoSecundario");
    public string TextoEstado =>
        esRetiro ? (Entregado ? "Retirado" : "Falta escanear")
        : Entregado ? "Se entrega" : "No se entrega";
}
