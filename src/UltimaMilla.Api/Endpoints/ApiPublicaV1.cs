using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using UltimaMilla.Api.Seguridad;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Application.Envios.Queries.ListarEnvios;
using UltimaMilla.Application.Envios.Queries.ObtenerDetalleEnvio;
using UltimaMilla.Application.Retiros.Commands.CrearRetiro;
using UltimaMilla.Application.Retiros.Queries;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// API pública para comercios (opcional de 3 puntos): superficie estable y versionada (/api/v1) para que el
/// sistema de un comercio dé de alta y consulte envíos y retiros. Se autentica solo con la clave X-Api-Key
/// (no con cookie), lo que permite limitarla y documentarla aparte de la API interna del Portal y el Backoffice.
/// Usa los mismos casos de uso que los endpoints internos: no duplica reglas de negocio.
/// </summary>
public static class ApiPublicaV1
{
    public const string Documento = "comercios";

    public static IServiceCollection AddApiPublica(this IServiceCollection services)
    {
        services.AddApiVersioning(o =>
        {
            o.DefaultApiVersion = new ApiVersion(1, 0);
            // Los endpoints internos (/api/...) no llevan versión y siguen funcionando igual.
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ReportApiVersions = true;
            o.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        // Documento OpenAPI propio de la API pública: no incluye los endpoints internos.
        services.AddOpenApi(Documento, o =>
        {
            o.ShouldInclude = descripcion => descripcion.GroupName == Documento;

            o.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info = new OpenApiInfo
                {
                    Title = "API para comercios — Plataforma Multioperador de Última Milla",
                    Version = "1.0",
                    Description =
                        "Alta y consulta de envíos y retiros desde el sistema del comercio. " +
                        "Se autentica con la clave del comercio en el encabezado X-Api-Key, que se genera desde el " +
                        "Portal (Usuarios > Clave de API) y se muestra una sola vez. " +
                        "Ambiente de pruebas: la clave de pruebas (Usuarios > Ver clave de pruebas) valida los pedidos igual " +
                        "que la real pero no guarda nada, y su límite es menor. " +
                        "Límite de pedidos por minuto y por clave: 120 con clave real, 30 con clave de pruebas; " +
                        "al superarlo se responde 429 con la cabecera Retry-After."
                };

                documento.Components ??= new OpenApiComponents();
                documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                documento.Components.SecuritySchemes["ClaveApi"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    Name = ClaveApiAuthenticationHandler.Encabezado,
                    In = ParameterLocation.Header,
                    Description = "Clave de API del usuario del comercio."
                };

                // El segmento de versión va fijo en la documentación: /api/v1/..., no /api/v{version}/...
                var rutas = new OpenApiPaths();
                foreach (var (ruta, item) in documento.Paths)
                    rutas[ruta.Replace("v{version}", "v1", StringComparison.Ordinal)] = item;
                documento.Paths = rutas;

                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication MapApiPublicaV1(this WebApplication app)
    {
        // Documentación navegable en /docs, leyendo el contrato /openapi/comercios.json.
        app.MapScalarApiReference("/docs", options =>
        {
            options.WithTitle("API para comercios");
            options.AddDocument(Documento);
        });

        var v1 = app.NewVersionedApi("Comercios")
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(1.0)
            .WithGroupName(Documento);

        // POST /api/v1/envios → alta de un envío. Responde 201 con el id y el token de seguimiento.
        // Con la clave de pruebas se ejecuta igual (validaciones, tarifa) pero se descarta al final: no queda nada.
        v1.MapPost("/envios", async (
            HttpContext http,
            CrearEnvioCommand command,
            CrearEnvioHandler handler,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var pruebas = ClaveApiAuthenticationHandler.EsEntornoDePruebas(http);

            var resultado = pruebas
                ? await EjecutarSinGuardarAsync(db, () => handler.Handle(command, ct), ct)
                : await handler.Handle(command, ct);

            return Results.Created(
                $"/api/v1/envios/{resultado.EnvioId}",
                new { id = resultado.EnvioId, tokenSeguimiento = resultado.TokenSeguimiento, entorno = pruebas ? "pruebas" : "produccion" });
        })
        .WithTags("Envíos")
        .WithSummary("Da de alta un envío")
        .Produces(StatusCodes.Status201Created)
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // GET /api/v1/envios → envíos visibles para la clave.
        // Con la clave de pruebas no se muestran datos reales: la lista viene vacía.
        v1.MapGet("/envios", async (
            HttpContext http,
            ListarEnviosHandler handler,
            CancellationToken ct) =>
            ClaveApiAuthenticationHandler.EsEntornoDePruebas(http)
                ? Results.Ok(Array.Empty<object>())
                : Results.Ok(await handler.Handle(new ListarEnviosQuery(null), ct)))
        .WithTags("Envíos")
        .WithSummary("Lista los envíos")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // GET /api/v1/envios/{id} → detalle y estado de un envío.
        v1.MapGet("/envios/{envioId:guid}", async (
            HttpContext http,
            Guid envioId,
            ObtenerDetalleEnvioHandler handler,
            CancellationToken ct) =>
            ClaveApiAuthenticationHandler.EsEntornoDePruebas(http)
                ? SinDatosEnPruebas()
                : Results.Ok(await handler.Handle(new ObtenerDetalleEnvioQuery(envioId), ct)))
        .WithTags("Envíos")
        .WithSummary("Consulta el detalle de un envío")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // POST /api/v1/retiros → solicitud de retiro (RF 37). Con la clave de pruebas no se guarda.
        v1.MapPost("/retiros", async (
            HttpContext http,
            CrearRetiroCommand command,
            CrearRetiroHandler handler,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var pruebas = ClaveApiAuthenticationHandler.EsEntornoDePruebas(http);

            var id = pruebas
                ? await EjecutarSinGuardarAsync(db, () => handler.Handle(command, ct), ct)
                : await handler.Handle(command, ct);

            return Results.Created($"/api/v1/retiros/{id}", new { id, entorno = pruebas ? "pruebas" : "produccion" });
        })
        .WithTags("Retiros")
        .WithSummary("Solicita un retiro")
        .Produces(StatusCodes.Status201Created)
        .RequireAuthorization(PoliticaConClave(Perfiles.PersonalComercio));

        // GET /api/v1/retiros → retiros del comercio.
        v1.MapGet("/retiros", async (
            HttpContext http,
            ConsultarRetirosHandler handler,
            CancellationToken ct) =>
            ClaveApiAuthenticationHandler.EsEntornoDePruebas(http)
                ? Results.Ok(Array.Empty<object>())
                : Results.Ok(await handler.ListarAsync(ct)))
        .WithTags("Retiros")
        .WithSummary("Lista los retiros")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // GET /api/v1/retiros/{id} → detalle de un retiro.
        v1.MapGet("/retiros/{retiroId:guid}", async (
            HttpContext http,
            Guid retiroId,
            ConsultarRetirosHandler handler,
            CancellationToken ct) =>
        {
            if (ClaveApiAuthenticationHandler.EsEntornoDePruebas(http))
                return SinDatosEnPruebas();

            var retiro = await handler.ObtenerAsync(retiroId, ct);

            return retiro is null ? Results.NotFound() : Results.Ok(retiro);
        })
        .WithTags("Retiros")
        .WithSummary("Consulta el detalle de un retiro")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        return app;
    }

    /// <summary>
    /// Ejecuta la operación completa (validaciones, tarifa, eventos) dentro de una transacción que siempre se
    /// deshace: es lo que hace que la clave de pruebas se comporte como la real sin dejar datos.
    /// </summary>
    private static async Task<T> EjecutarSinGuardarAsync<T>(AppDbContext db, Func<Task<T>> operacion, CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        try
        {
            return await operacion();
        }
        finally
        {
            await transaccion.RollbackAsync(CancellationToken.None);
        }
    }

    private static IResult SinDatosEnPruebas() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "En el ambiente de pruebas no se guardan datos.",
            detail: "Los pedidos hechos con la clave de pruebas se validan pero no se almacenan, así que no hay nada que consultar.");

    /// <summary>
    /// La clave de pruebas solo sirve en /api/v1: sin esto podría llamar a los endpoints internos, que sí guardan.
    /// Va después de UseAuthentication.
    /// </summary>
    public static IApplicationBuilder UseAislamientoDePruebas(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            if (ClaveApiAuthenticationHandler.EsLlamadaConClave(contexto) &&
                contexto.Request.Path.StartsWithSegments("/api/v1"))
            {
                contexto.Response.Headers["X-Entorno"] = ClaveApiAuthenticationHandler.EsEntornoDePruebas(contexto)
                    ? ClaveApiAuthenticationHandler.EntornoPruebas
                    : ClaveApiAuthenticationHandler.EntornoProduccion;
            }

            if (ClaveApiAuthenticationHandler.EsEntornoDePruebas(contexto) &&
                !contexto.Request.Path.StartsWithSegments("/api/v1"))
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                await Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "La clave de pruebas solo sirve en /api/v1.")
                    .ExecuteAsync(contexto);
                return;
            }

            await siguiente();
        });

    /// <summary>Solo con clave de API (sin cookie) y con alguno de los perfiles indicados.</summary>
    private static Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> PoliticaConClave(
        string[] perfiles) =>
        politica => politica
            .AddAuthenticationSchemes(ClaveApiAuthenticationHandler.Esquema)
            .RequireRole(perfiles);
}