using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Pages;

public partial class SeleccionOperadorPage : ContentPage
{
    private readonly SeleccionOperadorViewModel _vm;

    // El ViewModel llega por inyección de dependencias (registrado en MauiProgram).
    public SeleccionOperadorPage(SeleccionOperadorViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Igual que ScanPage en la demo: la página solo avisa que apareció; la lógica está en el ViewModel.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.OnAppearing();
    }
}
