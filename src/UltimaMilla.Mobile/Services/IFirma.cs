using UltimaMilla.Mobile.Pages;

namespace UltimaMilla.Mobile.Services;

/// <summary>Firma del receptor en pantalla (RF 20).</summary>
public interface IFirma
{
    /// <summary>Abre la pantalla de firma, guarda la firma en el teléfono y devuelve su referencia. Null si se cancela.</summary>
    Task<string?> PedirFirmaAsync(Guid idEvento, string receptor);
}

public sealed class Firma : IFirma
{
    public async Task<string?> PedirFirmaAsync(Guid idEvento, string receptor)
    {
        var pagina = new FirmaPage(receptor);
        await Shell.Current.Navigation.PushModalAsync(pagina);

        var svg = await pagina.Resultado;
        if (svg is null)
            return null;

        // Igual que la foto: se guarda en la carpeta privada de la app y al servidor viaja la referencia.
        var carpeta = Path.Combine(FileSystem.AppDataDirectory, "firmas");
        Directory.CreateDirectory(carpeta);
        var nombre = $"{idEvento}.svg";
        await File.WriteAllTextAsync(Path.Combine(carpeta, nombre), svg);

        return nombre;
    }
}
