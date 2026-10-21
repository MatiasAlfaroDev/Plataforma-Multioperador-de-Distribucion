using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Pages;

public partial class EnviosPage : ContentPage
{
    private readonly EnviosViewModel _vm;

    public EnviosPage(EnviosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Igual que ScanPage en la demo: la página solo avisa; la lógica está en el ViewModel.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.OnDisappearing();
    }
}
