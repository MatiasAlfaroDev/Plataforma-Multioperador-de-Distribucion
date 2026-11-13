using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace UltimaMilla.Web.Portal;

/// <summary>
/// Seguridad web del Portal (RNF 6.3): cabeceras de seguridad y protección del seguimiento público
/// contra la enumeración de tokens. El token ya es aleatorio de 256 bits y la base guarda solo su hash;
/// además se limita cuántas páginas de seguimiento puede pedir cada visitante por minuto.
/// </summary>
public static class CabecerasDeSeguridad
{
    /// <summary>Consultas de seguimiento por visitante (IP) y por minuto. Se cambia con SeguridadWeb:SeguimientoPorMinuto.</summary>
    private const int SeguimientoPorMinutoPorDefecto = 30;

    public static IServiceCollection AddSeguridadWebPortal(this IServiceCollection services, IConfiguration configuration)
    {
        var porMinuto = configuration.GetValue("SeguridadWeb:SeguimientoPorMinuto", SeguimientoPorMinutoPorDefecto);

        // El Portal se publica detrás de Traefik: la IP del visitante llega en X-Forwarded-For (último valor).
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            o.ForwardLimit = 1;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                contexto.Request.Path.StartsWithSegments("/seguimiento")
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        $"seguimiento:{contexto.Connection.RemoteIpAddress}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = porMinuto,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        })
                    : RateLimitPartition.GetNoLimiter("resto"));
        });

        return services;
    }

    /// <summary>Va al principio del pipeline, antes de autenticar.</summary>
    public static WebApplication UseSeguridadWebPortal(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use((contexto, siguiente) =>
        {
            Agregar(contexto);
            return siguiente();
        });
        app.UseRateLimiter();
        return app;
    }

    private static void Agregar(HttpContext contexto)
    {
        contexto.Response.OnStarting(() =>
        {
            var h = contexto.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            // El enlace de seguimiento lleva el token en la URL: no se manda a otros sitios como "referer".
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            // No restringe los scripts y estilos propios de Blazor; sí impide que el Portal se meta en un iframe
            // ajeno (clickjacking), que se cambie la base de los enlaces y que los formularios se envíen a otro sitio.
            h["Content-Security-Policy"] = "frame-ancestors 'none'; base-uri 'self'; object-src 'none'; form-action 'self'";

            if (contexto.Request.IsHttps ||
                string.Equals(contexto.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase))
            {
                h["Strict-Transport-Security"] = "max-age=31536000";
            }

            return Task.CompletedTask;
        });
    }
}
