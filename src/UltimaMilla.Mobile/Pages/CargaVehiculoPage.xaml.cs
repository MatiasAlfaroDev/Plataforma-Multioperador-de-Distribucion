using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Pages;

public partial class CargaVehiculoPage : ContentPage
{
    private readonly CargaVehiculoViewModel _vm;

    public CargaVehiculoPage(CargaVehiculoViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Cada vez que se abre la pantalla se descargan las hojas de hoy.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.CargarCommand.Execute(null);
    }

    // Después de cada código, el cursor vuelve al campo para escanear el siguiente sin tocar la pantalla.
    private void AlConfirmarCodigo(object? sender, EventArgs e) => CampoCodigo.Focus();
}
