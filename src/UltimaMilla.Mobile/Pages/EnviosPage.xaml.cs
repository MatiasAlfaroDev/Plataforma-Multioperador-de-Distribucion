using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Pages;

public partial class EnviosPage : ContentPage
{
    public EnviosPage(EnviosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
