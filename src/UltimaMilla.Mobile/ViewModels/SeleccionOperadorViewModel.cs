using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// ViewModel de la primera pantalla. PROVISORIO hasta el inicio de sesión (15/10):
/// el repartidor elige su operador. Después el operador saldrá del token del usuario.
/// </summary>
public partial class SeleccionOperadorViewModel : ObservableObject
{
    private readonly IEnviosApi _api;

    public SeleccionOperadorViewModel(IEnviosApi api)
    {
        _api = api;
    }

    /// <summary>Propiedad observable: la lista de la pantalla se actualiza sola al agregar o quitar.</summary>
    public ObservableCollection<OperadorItem> Operadores { get; } = new();

    [ObservableProperty]
    private bool estaCargando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? error;

    public bool HayError => !string.IsNullOrEmpty(Error);

    /// <summary>La página lo llama al aparecer (como ScanPage llama a _vm.OnAppearing() en la demo).</summary>
    public void OnAppearing()
    {
        if (Operadores.Count == 0)
            CargarCommand.Execute(null);
    }

    /// <summary>Genera CargarCommand (ICommand). Lo usan OnAppearing y el gesto de refrescar.</summary>
    [RelayCommand]
    private async Task CargarAsync()
    {
        EstaCargando = true;
        Error = null;
        try
        {
            var lista = await _api.ListarOperadoresAsync();
            Operadores.Clear();
            foreach (var operador in lista)
                Operadores.Add(operador);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Error = "No se pudo conectar con la API. ¿Está levantado docker compose?";
        }
        finally
        {
            EstaCargando = false;
        }
    }

    /// <summary>Genera ElegirCommand. Navega a la lista de envíos pasando el operador elegido.</summary>
    [RelayCommand]
    private Task ElegirAsync(OperadorItem operador) =>
        Shell.Current.GoToAsync("envios", new Dictionary<string, object> { ["operador"] = operador });
}
