using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>Pantalla de inicio de sesión del repartidor.</summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthApi _auth;
    private readonly IEnviosApi _api;

    public LoginViewModel(IAuthApi auth, IEnviosApi api)
    {
        _auth = auth;
        _api = api;
    }

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool estaCargando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? error;

    public bool HayError => !string.IsNullOrEmpty(Error);

    [RelayCommand]
    private async Task IniciarSesionAsync()
    {
        Error = null;
        EstaCargando = true;
        try
        {
            // ① Email y contraseña. Si están bien, la API deja la cookie de sesión.
            var usuario = await _auth.IniciarSesionAsync(Email, Password);
            if (usuario is null)
            {
                Error = "Email o contraseña incorrectos.";
                return;
            }

            // ② ¿Para qué operadores trabaja? Se prefiere la membresía de repartidor.
            var membresias = await _auth.ListarMembresiasAsync();
            var membresia = membresias.FirstOrDefault(m => m.Perfil == "Repartidor") ?? membresias.FirstOrDefault();
            if (membresia is null)
            {
                Error = "Tu usuario no trabaja para ningún operador.";
                return;
            }

            // ③ Elegirlo: la cookie nueva ya lleva el operador y el perfil.
            await _auth.SeleccionarOperadorAsync(membresia.Id);

            // Nombre y color del operador para la pantalla de la ruta.
            var operadores = await _api.ListarOperadoresAsync();
            var operador = operadores.FirstOrDefault(o => o.Id == membresia.OperadorId);
            if (operador is null)
            {
                Error = "No se encontró el operador.";
                return;
            }

            Password = string.Empty;
            await Shell.Current.GoToAsync("//operador/envios",
                new Dictionary<string, object> { ["operador"] = operador });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Error = "No se pudo conectar con el servidor.";
        }
        finally
        {
            EstaCargando = false;
        }
    }

    /// <summary>Sin señal no se puede iniciar sesión: se trabaja con la ruta guardada en el teléfono.</summary>
    [RelayCommand]
    private Task TrabajarSinConexionAsync() => Shell.Current.GoToAsync("//operador");
}