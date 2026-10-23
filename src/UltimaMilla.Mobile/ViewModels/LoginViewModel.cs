using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Services;

namespace UltimaMilla.Mobile.ViewModels;

/// <summary>Pantalla de inicio de sesión del repartidor.</summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthApi _auth;

    public LoginViewModel(IAuthApi auth)
    {
        _auth = auth;
    }

    // Lo que escribe el usuario. [ObservableProperty] genera Email y Password, y avisa a la pantalla.
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

    // [RelayCommand] genera "IniciarSesionCommand" para enganchar al botón.
    [RelayCommand]
    private async Task IniciarSesionAsync()
    {
        Error = null;
        EstaCargando = true;
        try
        {
            var sesion = await _auth.IniciarSesionAsync(Email, Password);    

            if (sesion is null)
            {
                Error = "Email o contraseña incorrectos";                                 
                return;
            }

            // Por ahora vamos a la pantalla de operadores. En el paso 4 guardamos el token.
            await Shell.Current.GoToAsync("//operador");
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
}