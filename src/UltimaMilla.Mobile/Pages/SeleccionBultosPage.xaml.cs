using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Pages;

/// <summary>
/// Pantalla modal de la parada: "devuelve" qué bultos se entregan por <see cref="Resultado"/>,
/// o null si el repartidor canceló (igual que FirmaPage).
/// </summary>
public partial class SeleccionBultosPage : ContentPage
{
    private readonly SeleccionBultosViewModel _vm;
    private readonly TaskCompletionSource<SeleccionBultos?> _resultado = new();

    public SeleccionBultosPage(SeleccionBultosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public Task<SeleccionBultos?> Resultado => _resultado.Task;

    private async void AlContinuar(object? sender, EventArgs e)
    {
        var seleccion = _vm.Resultado();
        await Navigation.PopModalAsync();
        _resultado.TrySetResult(seleccion);
    }

    private async void AlCancelar(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _resultado.TrySetResult(null);
    }

    // Botón "atrás" de Android: cuenta como cancelar.
    protected override bool OnBackButtonPressed()
    {
        _resultado.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
