using UltimaMilla.Mobile.Pages;

namespace UltimaMilla.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Rutas a las que se navega con Shell.Current.GoToAsync("envios", ...).
        Routing.RegisterRoute("envios", typeof(EnviosPage));
    }
}
