using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Pages;

/// <summary>Rendición al volver al depósito (RF 22). Misma mecánica que la carga del vehículo, al revés.</summary>
public partial class RendicionPage : ContentPage
{
    private readonly RendicionViewModel _vm;

    public RendicionPage(RendicionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Cada vez que se abre: se sube lo pendiente y se descargan las hojas para rendir.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.CargarCommand.Execute(null);
    }

    // Después de cada código, el cursor vuelve al campo para escanear el siguiente.
    private void AlConfirmarCodigo(object? sender, EventArgs e) => CampoCodigo.Focus();
}
