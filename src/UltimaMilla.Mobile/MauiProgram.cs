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
        // Carga el motor SQLite con cifrado (SQLCipher) que trae el paquete SQLitePCLRaw.bundle_e_sqlcipher.
        SQLitePCL.Batteries_V2.Init();

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

        // Trabajo sin conexión: base local cifrada, repositorios y sincronización (una sola instancia para toda la app).
        builder.Services.AddSingleton<BaseDeDatosLocal>();
        builder.Services.AddSingleton<RutaRepository>();
        builder.Services.AddSingleton<EventosPendientesRepository>();
        builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
        builder.Services.AddSingleton<SincronizacionService>();
        builder.Services.AddSingleton<IDialogos, DialogosShell>();

        // MVVM: cada página recibe su ViewModel por constructor.
        builder.Services.AddTransient<SeleccionOperadorViewModel>();
        builder.Services.AddTransient<SeleccionOperadorPage>();
        builder.Services.AddTransient<EnviosViewModel>();
        builder.Services.AddTransient<EnviosPage>();

        var app = builder.Build();

        // Se crea al arrancar para que empiece a escuchar los cambios de conexión
        // y sincronice solo, aunque el repartidor no esté en la pantalla de la ruta.
        _ = app.Services.GetRequiredService<SincronizacionService>();

        return app;
    }
}
