namespace UltimaMilla.Mobile.Pages;

/// <summary>
/// Pantalla de firma. Se abre con Navigation.PushModalAsync y "devuelve" la firma por
/// <see cref="Resultado"/>: el SVG si se confirmó, o null si se canceló.
/// </summary>
public partial class FirmaPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _resultado = new();

    public FirmaPage(string receptor)
    {
        InitializeComponent();
        Indicacion.Text = $"Pedile a {receptor} que firme en el recuadro.";
    }

    public Task<string?> Resultado => _resultado.Task;

    private async void AlConfirmar(object? sender, EventArgs e)
    {
        if (Panel.EstaVacia)
        {
            await DisplayAlert("Firma", "Falta la firma del receptor.", "OK");
            return;
        }

        var svg = Panel.ExportarSvg();
        await Navigation.PopModalAsync();
        _resultado.TrySetResult(svg);
    }

    private void AlBorrar(object? sender, EventArgs e) => Panel.Limpiar();

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
