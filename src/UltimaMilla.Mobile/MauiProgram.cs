using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Pages;
using UltimaMilla.Mobile.Services;
using UltimaMilla.Mobile.ViewModels;

namespace UltimaMilla.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // HttpClient tipado hacia la API, configurado como en la demo BeatScan.
        builder.Services.AddHttpClient<IEnviosApi, EnviosApi>(c =>
        {
            c.BaseAddress = new Uri(Constants.BaseApiUrl);
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestVersion = System.Net.HttpVersion.Version11;
            c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        });

        // MVVM: cada página recibe su ViewModel por constructor.
        builder.Services.AddTransient<SeleccionOperadorViewModel>();
        builder.Services.AddTransient<SeleccionOperadorPage>();
        builder.Services.AddTransient<EnviosViewModel>();
        builder.Services.AddTransient<EnviosPage>();

        return builder.Build();
    }
}
