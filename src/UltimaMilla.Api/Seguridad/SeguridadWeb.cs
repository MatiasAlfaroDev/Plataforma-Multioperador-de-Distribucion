using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using StackExchange.Redis;

namespace UltimaMilla.Api.Seguridad;

/// <summary>
/// Seguridad web de la API (RNF 6.3): IP real del cliente detrás de Traefik, limitación de tasa,
/// CORS restringido y cabeceras de seguridad. Todo se configura en la sección "SeguridadWeb".
/// </summary>
public static class SeguridadWeb
{
    public const string SeccionConfiguracion = "SeguridadWeb";

    public static IServiceCollection AddSeguridadWeb(this IServiceCollection services, IConfiguration configuration)
    {
        var opciones = configuration.GetSection(SeccionConfiguracion).Get<OpcionesSeguridadWeb>() ?? new OpcionesSeguridadWeb();
        services.AddSingleton(opciones);

        // La API solo se publica a través de Traefik, que agrega la IP del cliente al final de X-Forwarded-For.
        // Con ForwardLimit = 1 se toma solo ese último valor (el que puso Traefik), así que un cliente
        // no puede hacerse pasar por otra IP mandando su propio encabezado.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            o.ForwardLimit = 1;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });

        // CORS: por defecto no se permite ningún origen externo. El Portal y el Backoffice llaman a la API por
        // el mismo origen (/api detrás de Traefik) o desde el servidor, así que no lo necesitan.
        services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (opciones.OrigenesPermitidos.Length > 0)
            {
                p.WithOrigins(opciones.OrigenesPermitidos)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        }));

        return services;
    }

    /// <summary>Va al principio del pipeline: antes de autenticar y antes de cualquier endpoint.</summary>
    public static WebApplication UseSeguridadWeb(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use((contexto, siguiente) =>
        {
            AgregarCabeceras(contexto, esApi: !contexto.Request.Path.StartsWithSegments("/docs"));
            return siguiente();
        });
        app.UseCors();

        return app;
    }

    /// <summary>
    /// Va después de UseAuthentication: así ya se sabe quién hace el pedido y cada usuario tiene su propio
    /// límite (sin eso, todos los que entran desde la misma red compartirían uno).
    /// </summary>
    public static WebApplication UseLimitacionDeTasa(this WebApplication app)
    {
        if (app.Services.GetRequiredService<OpcionesSeguridadWeb>().LimitarTasa)
            app.UseMiddleware<LimitadorDeTasaDistribuido>();

        return app;
    }

    /// <summary>
    /// Cabeceras de seguridad. Para la API (respuestas JSON) la política de contenido no permite cargar nada;
    /// para las páginas del Portal y el Backoffice se usa la versión que no restringe sus propios scripts y estilos.
    /// </summary>
    public static void AgregarCabeceras(HttpContext contexto, bool esApi)
    {
        contexto.Response.OnStarting(() =>
        {
            var h = contexto.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            // El enlace de seguimiento lleva el token en la URL: no se manda a otros sitios como "referer".
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            h["Content-Security-Policy"] = esApi
                ? "default-src 'none'; frame-ancestors 'none'"
                : "frame-ancestors 'none'; base-uri 'self'; object-src 'none'; form-action 'self'";

            // HSTS solo cuando el cliente entró por HTTPS (en la nube Traefik termina TLS y avisa con X-Forwarded-Proto).
            if (contexto.Request.IsHttps ||
                string.Equals(contexto.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase))
            {
                h["Strict-Transport-Security"] = "max-age=31536000";
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// A qué "balde" cuenta cada pedido. Los intentos de acceso y el seguimiento público se cuentan por IP
    /// (son anónimos y son los que se usan para adivinar contraseñas o tokens); las llamadas con clave de API,
    /// por clave y con su propio límite (API pública); el resto, por usuario (o por IP si no hay sesión).
    /// Las sondas de salud de Traefik no se limitan (null).
    /// </summary>
    public static CuotaDeTasa? Cuota(HttpContext contexto, OpcionesSeguridadWeb opciones)
    {
        var ruta = contexto.Request.Path;
        var ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

        if (ruta.StartsWithSegments("/live") || ruta.StartsWithSegments("/ready") || ruta.StartsWithSegments("/health"))
            return null;

        if (ruta.StartsWithSegments("/api/auth/login") || ruta.StartsWithSegments("/api/auth/registro")
            || ruta.StartsWithSegments("/api/auth/registro-comercio") || ruta.StartsWithSegments("/api/auth/registro-operador"))
            return new CuotaDeTasa($"acceso:{ip}", opciones.AccesoPorMinuto);

        if (ruta.StartsWithSegments("/api/seguimiento"))
            return new CuotaDeTasa($"seguimiento:{ip}", opciones.SeguimientoPorMinuto);

        var usuario = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (usuario is not null && ClaveApiAuthenticationHandler.EsEntornoDePruebas(contexto))
            return new CuotaDeTasa($"clave-pruebas:{usuario}", opciones.ClavePruebasPorMinuto);

        // Limitación diferenciada para la API pública: cada clave de comercio tiene su propio cupo.
        if (usuario is not null && contexto.User.Identity?.AuthenticationType == ClaveApiAuthenticationHandler.Esquema)
            return new CuotaDeTasa($"clave:{usuario}", opciones.ClaveApiPorMinuto);

        return usuario is null
            ? new CuotaDeTasa($"general:{ip}", opciones.GeneralPorMinuto)
            : new CuotaDeTasa($"usuario:{usuario}", opciones.GeneralPorMinuto);
    }
}

/// <summary>Un balde de la limitación de tasa: su identificador y cuántos pedidos permite por minuto.</summary>
public sealed record CuotaDeTasa(string Clave, int PorMinuto);

/// <summary>
/// Limitación de tasa con el contador en Redis (aclaración 3.1). El limitador que trae ASP.NET Core cuenta en
/// cada instancia, así que con dos instancias detrás del balanceador el límite real sería el doble. Acá todas
/// las instancias suman en el mismo contador: ventana fija de un minuto, un INCR atómico por pedido.
/// Si Redis no está disponible, se deja pasar el pedido (igual que el caché, el sistema sigue funcionando).
/// </summary>
public sealed class LimitadorDeTasaDistribuido(
    RequestDelegate siguiente,
    OpcionesSeguridadWeb opciones,
    IServiceProvider servicios,
    ILogger<LimitadorDeTasaDistribuido> logger)
{
    // INCR y, si es el primer pedido de la ventana, el vencimiento de la clave. En un script para que sea atómico.
    private const string Script =
        "local n = redis.call('INCR', KEYS[1]) if n == 1 then redis.call('EXPIRE', KEYS[1], ARGV[1]) end return n";

    public async Task InvokeAsync(HttpContext contexto)
    {
        var cuota = SeguridadWeb.Cuota(contexto, opciones);
        var redis = servicios.GetService<IConnectionMultiplexer>();

        if (cuota is null || redis is null || !redis.IsConnected)
        {
            await siguiente(contexto);
            return;
        }

        var ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ventana = ahora / 60;
        long cuenta;

        try
        {
            var resultado = await redis.GetDatabase().ScriptEvaluateAsync(
                Script,
                [new RedisKey($"limite-tasa:{cuota.Clave}:{ventana}")],
                [70]);
            cuenta = (long)resultado;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Limitación de tasa sin Redis: el pedido pasa sin contar.");
            await siguiente(contexto);
            return;
        }

        if (cuenta > cuota.PorMinuto)
        {
            var segundosRestantes = 60 - (ahora % 60);
            contexto.Response.Headers.RetryAfter = segundosRestantes.ToString(System.Globalization.CultureInfo.InvariantCulture);

            await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Demasiadas solicitudes. Probá de nuevo en unos segundos.")
                .ExecuteAsync(contexto);
            return;
        }

        await siguiente(contexto);
    }
}

/// <summary>Valores por defecto pensados para no molestar el uso normal ni la demo; se cambian por configuración.</summary>
public sealed class OpcionesSeguridadWeb
{
    /// <summary>Permite apagar la limitación (por ejemplo para una prueba de carga) sin tocar código.</summary>
    public bool LimitarTasa { get; set; } = true;

    /// <summary>Login y registros, por IP: frena la prueba de contraseñas por fuerza bruta.</summary>
    public int AccesoPorMinuto { get; set; } = 20;

    /// <summary>
    /// Seguimiento público, por IP. Es generoso porque las consultas del Portal llegan todas desde su contenedor;
    /// el límite estricto por visitante se aplica en el propio Portal.
    /// </summary>
    public int SeguimientoPorMinuto { get; set; } = 300;

    /// <summary>El resto de la API, por usuario (o por IP sin sesión).</summary>
    public int GeneralPorMinuto { get; set; } = 600;

    /// <summary>API pública: cada clave de comercio, por minuto. Es menor que el de un usuario porque son integraciones automáticas.</summary>
    public int ClaveApiPorMinuto { get; set; } = 120;
    
    /// <summary>API pública, clave del ambiente de pruebas: menor que la real, porque no guarda nada.</summary>
    public int ClavePruebasPorMinuto { get; set; } = 30;
    
    /// <summary>Orígenes de navegador que pueden llamar a la API desde otro dominio. Vacío: ninguno.</summary>
    public string[] OrigenesPermitidos { get; set; } = [];
}
