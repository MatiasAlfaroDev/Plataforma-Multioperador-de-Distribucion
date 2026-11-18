using Microsoft.Extensions.DependencyInjection;

namespace UltimaMilla.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // Siempre en claro, como Portal y Backoffice: el modo oscuro del sistema no cambia la app.
            UserAppTheme = AppTheme.Light;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}