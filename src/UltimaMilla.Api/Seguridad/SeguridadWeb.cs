using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

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

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (contexto, ct) =>
            {
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(espera.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Demasiadas solicitudes. Probá de nuevo en unos segundos.")
                    .ExecuteAsync(contexto.HttpContext);
            };

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                Particion(contexto, opciones));
        });

        return services;
    }

    /// <summary>Va al principio del pipeline: antes de autenticar y antes de cualquier endpoint.</summary>
    public static WebApplication UseSeguridadWeb(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use((contexto, siguiente) =>
        {
            AgregarCabeceras(contexto, esApi: true);
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
            app.UseRateLimiter();

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
    /// (son anónimos y son los que se usan para adivinar contraseñas o tokens); el resto, por usuario
    /// (o por IP si no hay sesión). Las sondas de salud de Traefik no se limitan.
    /// </summary>
    public static RateLimitPartition<string> Particion(HttpContext contexto, OpcionesSeguridadWeb opciones)
    {
        var ruta = contexto.Request.Path;
        var ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

        if (ruta.StartsWithSegments("/live") || ruta.StartsWithSegments("/ready") || ruta.StartsWithSegments("/health"))
            return RateLimitPartition.GetNoLimiter("sondas");

        if (ruta.StartsWithSegments("/api/auth/login") || ruta.StartsWithSegments("/api/auth/registro")
            || ruta.StartsWithSegments("/api/auth/registro-comercio") || ruta.StartsWithSegments("/api/auth/registro-operador"))
            return Ventana($"acceso:{ip}", opciones.AccesoPorMinuto);

        if (ruta.StartsWithSegments("/api/seguimiento"))
            return Ventana($"seguimiento:{ip}", opciones.SeguimientoPorMinuto);

        var usuario = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ventana(usuario is null ? $"general:{ip}" : $"usuario:{usuario}", opciones.GeneralPorMinuto);
    }

    private static RateLimitPartition<string> Ventana(string clave, int permitidosPorMinuto) =>
        RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitidosPorMinuto,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
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

    /// <summary>Orígenes de navegador que pueden llamar a la API desde otro dominio. Vacío: ninguno.</summary>
    public string[] OrigenesPermitidos { get; set; } = [];
}
