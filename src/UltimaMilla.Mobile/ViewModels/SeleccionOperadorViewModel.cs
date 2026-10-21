using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// ViewModel de la primera pantalla. PROVISORIO hasta el inicio de sesión: el repartidor
/// elige su operador. Con conexión trae los operadores de la API y los guarda en SQLite;
/// sin conexión muestra los guardados.
/// </summary>
public partial class SeleccionOperadorViewModel : ObservableObject
{
    private readonly IEnviosApi _api;
    private readonly RutaRepository _local;

    public SeleccionOperadorViewModel(IEnviosApi api, RutaRepository local)
    {
        _api = api;
        _local = local;
    }

    public ObservableCollection<OperadorItem> Operadores { get; } = new();

    [ObservableProperty]
    private bool estaCargando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAviso))]
    private string? aviso;

    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool HayAviso => !string.IsNullOrEmpty(Aviso);

    /// <summary>La página lo llama al aparecer (como ScanPage llama a _vm.OnAppearing() en la demo).</summary>
    public void OnAppearing()
    {
        if (Operadores.Count == 0)
            CargarCommand.Execute(null);
    }

    [RelayCommand]
    private async Task CargarAsync()
    {
        EstaCargando = true;
        Error = null;
        Aviso = null;
        try
        {
            IReadOnlyList<OperadorItem> lista;
            try
            {
                lista = await _api.ListarOperadoresAsync();
                await _local.GuardarOperadoresAsync(lista);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lista = await _local.ListarOperadoresAsync();
                if (lista.Count == 0)
                    Error = "Sin conexión y sin datos guardados. Conectate una vez para descargar los operadores.";
                else
                    Aviso = "Sin conexión: mostrando los operadores guardados en el teléfono.";
            }

            Operadores.Clear();
            foreach (var operador in lista)
                Operadores.Add(operador);
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
    private Task ElegirAsync(OperadorItem operador) =>
        Shell.Current.GoToAsync("envios", new Dictionary<string, object> { ["operador"] = operador });
}
