using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}