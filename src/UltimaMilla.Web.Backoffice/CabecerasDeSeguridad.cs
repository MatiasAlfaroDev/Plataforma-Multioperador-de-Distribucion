namespace UltimaMilla.Web.Backoffice;

/// <summary>Cabeceras de seguridad del Backoffice (RNF 6.3). Mismo criterio que el Portal.</summary>
public static class CabecerasDeSeguridad
{
    /// <summary>Va al principio del pipeline, antes de autenticar.</summary>
    public static WebApplication UseCabecerasDeSeguridad(this WebApplication app)
    {
        app.Use((contexto, siguiente) =>
        {
            contexto.Response.OnStarting(() =>
            {
                var h = contexto.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                // No restringe los scripts y estilos propios de las páginas; sí el iframe ajeno (clickjacking),
                // el cambio de la base de los enlaces y el envío de formularios a otro sitio.
                h["Content-Security-Policy"] = "frame-ancestors 'none'; base-uri 'self'; object-src 'none'; form-action 'self'";

                if (contexto.Request.IsHttps ||
                    string.Equals(contexto.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase))
                {
                    h["Strict-Transport-Security"] = "max-age=31536000";
                }

                return Task.CompletedTask;
            });

            return siguiente();
        });

        return app;
    }
}
