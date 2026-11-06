using UltimaMilla.Mobile.Models;
using UltimaMilla.Mobile.Pages;
using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile.Services;

/// <summary>Pantalla de la parada donde se escanean los bultos que se entregan (RF 32).</summary>
public interface IElegirBultos
{
    /// <summary>Null si el repartidor canceló.</summary>
    Task<SeleccionBultos?> ElegirAsync(string referencia, IReadOnlyList<BultoItem> bultos);
}

public sealed class ElegirBultos(ILectorCodigos lector) : IElegirBultos
{
    public async Task<SeleccionBultos?> ElegirAsync(string referencia, IReadOnlyList<BultoItem> bultos)
    {
        var pagina = new SeleccionBultosPage(new SeleccionBultosViewModel(referencia, bultos, lector));
        await Shell.Current.Navigation.PushModalAsync(pagina);
        return await pagina.Resultado;
    }
}
