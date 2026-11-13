using StackExchange.Redis;

namespace UltimaMilla.Api.Seguridad;

/// <summary>
/// Limitación de tasa compartida entre réplicas (aclaración 3.1): el limitador de ASP.NET Core cuenta
/// en cada instancia y con dos réplicas el límite real sería el doble. Acá el contador vive en Redis.
/// Protege lo que se llama sin sesión: el seguimiento público y el login.
/// </summary>
/// <remarks>
/// ponytail: ventana fija de un minuto por IP; en el borde de la ventana puede pasar el doble en ráfaga.
/// Si molesta, ventana deslizante con un sorted set. Sin Redis deja pasar: no corta el servicio.
/// </remarks>
public static class LimiteDeTasa
{
    private static readonly (string Prefijo, int PorMinuto)[] Reglas =
    [
        // La página de seguimiento del Portal consulta a la API desde el servidor: todos sus visitantes
        // cuentan como la IP del Portal, por eso el límite es holgado. Frena a un cliente que pegue directo.
        ("/api/seguimiento", 600),
        ("/api/auth/login", 10)
    ];

    public static IApplicationBuilder UseLimiteDeTasa(this IApplicationBuilder app) =>
        // La misma llave que el limitador por usuario: SeguridadWeb:LimitarTasa=false lo apaga para una prueba de carga.
        !app.ApplicationServices.GetRequiredService<OpcionesSeguridadWeb>().LimitarTasa ? app : app.Use(async (context, next) =>
        {
            var ruta = context.Request.Path.Value ?? string.Empty;
            var regla = Reglas.FirstOrDefault(r => ruta.StartsWith(r.Prefijo, StringComparison.OrdinalIgnoreCase));

            if (regla.Prefijo is not null && await Excedido(context, regla.Prefijo, regla.PorMinuto))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = "60";
                return;
            }

            await next();
        });

    private static async Task<bool> Excedido(HttpContext context, string prefijo, int porMinuto)
    {
        var redis = context.RequestServices.GetService<IConnectionMultiplexer>();
        if (redis is null)
            return false;

        // Detrás de Traefik la IP del cliente es la última de X-Forwarded-For: la agrega Traefik, y las
        // anteriores las puede inventar el cliente para esquivar el límite.
        var ip = context.Request.Headers["X-Forwarded-For"].ToString().Split(',')[^1].Trim();
        if (string.IsNullOrEmpty(ip))
            ip = context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

        var ventana = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var clave = $"limite:{prefijo}:{ip}:{ventana}";

        try
        {
            var db = redis.GetDatabase();
            var cuenta = await db.StringIncrementAsync(clave);
            if (cuenta == 1)
                await db.KeyExpireAsync(clave, TimeSpan.FromSeconds(70));

            return cuenta > porMinuto;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            return false;
        }
    }
}
