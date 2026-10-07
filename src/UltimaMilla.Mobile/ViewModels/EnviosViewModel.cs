using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>
/// ViewModel de la lista de envíos del operador elegido. En el 22/10 pasa a mostrar
/// la hoja de ruta del repartidor, guardada en SQLite para trabajar sin conexión.
/// </summary>
public partial class EnviosViewModel : ObservableObject, IQueryAttributable
{
    private readonly IEnviosApi _api;

    public EnviosViewModel(IEnviosApi api)
    {
        _api = api;
    }

    public ObservableCollection<EnvioItem> Envios { get; } = new();

    [ObservableProperty]
    private OperadorItem? operador;

    [ObservableProperty]
    private bool estaCargando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? error;

    public bool HayError => !string.IsNullOrEmpty(Error);

    /// <summary>Shell llama a este método con los parámetros de la navegación.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("operador", out var valor) && valor is OperadorItem elegido)
        {
            Operador = elegido;
            CargarCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task CargarAsync()
    {
        if (Operador is null)
            return;

        EstaCargando = true;
        Error = null;
        try
        {
            var lista = await _api.ListarEnviosAsync(Operador.Id);
            Envios.Clear();
            foreach (var envio in lista)
                Envios.Add(envio);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Error = "No se pudo conectar con la API.";
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
