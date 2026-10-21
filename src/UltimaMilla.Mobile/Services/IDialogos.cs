namespace UltimaMilla.Mobile.Services;

/// <summary>
/// Ventanas emergentes detrás de una interfaz, para que los ViewModels no dependan
/// de la pantalla (igual que IErrorHandler / ModalErrorHandler en la demo BeatScan).
/// Si Visual Studio marca DisplayAlert o DisplayActionSheet como "obsoleto" (.NET 10), es solo
/// un aviso: funciona igual. Se pueden cambiar por DisplayAlertAsync / DisplayActionSheetAsync.
/// </summary>
public interface IDialogos
{
    Task<string?> PedirTextoAsync(string titulo, string mensaje);
    Task<string?> ElegirAsync(string titulo, IReadOnlyList<string> opciones);
    Task AvisarAsync(string titulo, string mensaje);
}

public sealed class DialogosShell : IDialogos
{
    private static Page? PaginaActual => Shell.Current?.CurrentPage;

    public async Task<string?> PedirTextoAsync(string titulo, string mensaje)
    {
        if (PaginaActual is not { } pagina) return null;
        var texto = await pagina.DisplayPromptAsync(titulo, mensaje, "Aceptar", "Cancelar");
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    public async Task<string?> ElegirAsync(string titulo, IReadOnlyList<string> opciones)
    {
        if (PaginaActual is not { } pagina) return null;
        var elegido = await pagina.DisplayActionSheet(titulo, "Cancelar", null, opciones.ToArray());
        return opciones.Contains(elegido) ? elegido : null;
    }

    public Task AvisarAsync(string titulo, string mensaje) =>
        PaginaActual?.DisplayAlert(titulo, mensaje, "OK") ?? Task.CompletedTask;
}
