using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// Entrega parcial (RF 32): en la parada, el repartidor escanea (o toca) los bultos que entrega.
/// Los que quedan sin marcar son los no entregados.
/// </summary>
public partial class SeleccionBultosViewModel : ObservableObject
{
    private readonly ILectorCodigos _lector;

    public SeleccionBultosViewModel(string referencia, IReadOnlyList<BultoItem> bultos, ILectorCodigos lector)
    {
        Referencia = referencia;
        _lector = lector;
        foreach (var bulto in bultos)
            Bultos.Add(new BultoParada(bulto));
    }

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
        Entregados == 0 ? "Marcá los bultos que entregás"
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
            MostrarMensaje($"{leido} no es de este envío.", alerta: true);
        else if (bulto.Entregado)
            MostrarMensaje($"{leido} ya estaba marcado.", alerta: false);
        else
        {
            bulto.Entregado = true;
            MostrarMensaje($"✓ {leido} se entrega.", alerta: false);
        }

        ActualizarResumen();
    }

    /// <summary>Tocar un bulto lo marca o desmarca (por si la etiqueta no se puede leer).</summary>
    [RelayCommand]
    private void Alternar(BultoParada bulto)
    {
        bulto.Entregado = !bulto.Entregado;
        Mensaje = null;
        ActualizarResumen();
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
public partial class BultoParada(BultoItem bulto) : ObservableObject
{
    public BultoItem Bulto { get; } = bulto;
    public string Codigo => Bulto.Identificacion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icono), nameof(ColorIcono), nameof(TextoEstado))]
    private bool entregado;

    public string Icono => Entregado ? Iconos.TildeCirculo : Iconos.Caja;
    public Color ColorIcono => Paleta.De(Entregado ? "Exito" : "TextoSecundario");
    public string TextoEstado => Entregado ? "Se entrega" : "No se entrega";
}
