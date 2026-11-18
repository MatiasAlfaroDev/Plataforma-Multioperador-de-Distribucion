using UltimaMilla.Mobile.Pages;

namespace UltimaMilla.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pantallas que se abren desde la ruta ("//ruta") con Shell.Current.GoToAsync("carga"), etc.
        Routing.RegisterRoute("carga", typeof(CargaVehiculoPage));
        Routing.RegisterRoute("rendicion", typeof(RendicionPage));
    }
}
