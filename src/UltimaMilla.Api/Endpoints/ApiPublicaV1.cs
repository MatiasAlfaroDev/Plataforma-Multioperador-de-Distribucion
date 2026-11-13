using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using UltimaMilla.Api.Seguridad;
using UltimaMilla.Application.Envios.Commands.CrearEnvio;
using UltimaMilla.Application.Envios.Queries.ListarEnvios;
using UltimaMilla.Application.Envios.Queries.ObtenerDetalleEnvio;
using UltimaMilla.Application.Retiros.Commands.CrearRetiro;
using UltimaMilla.Application.Retiros.Queries;
using UltimaMilla.Application.Seguridad;

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
                        "Portal (Usuarios > Clave de API) y se muestra una sola vez."
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
        v1.MapPost("/envios", async (
            CrearEnvioCommand command,
            CrearEnvioHandler handler,
            CancellationToken ct) =>
        {
            var resultado = await handler.Handle(command, ct);

            return Results.Created(
                $"/api/v1/envios/{resultado.EnvioId}",
                new { id = resultado.EnvioId, tokenSeguimiento = resultado.TokenSeguimiento });
        })
        .WithTags("Envíos")
        .WithSummary("Da de alta un envío")
        .Produces(StatusCodes.Status201Created)
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // GET /api/v1/envios → envíos visibles para la clave.
        v1.MapGet("/envios", async (
            ListarEnviosHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.Handle(new ListarEnviosQuery(null), ct)))
        .WithTags("Envíos")
        .WithSummary("Lista los envíos")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // GET /api/v1/envios/{id} → detalle y estado de un envío.
        v1.MapGet("/envios/{envioId:guid}", async (
            Guid envioId,
            ObtenerDetalleEnvioHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.Handle(new ObtenerDetalleEnvioQuery(envioId), ct)))
        .WithTags("Envíos")
        .WithSummary("Consulta el detalle de un envío")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // POST /api/v1/retiros → solicitud de retiro (RF 37).
        v1.MapPost("/retiros", async (
            CrearRetiroCommand command,
            CrearRetiroHandler handler,
            CancellationToken ct) =>
        {
            var id = await handler.Handle(command, ct);

            return Results.Created($"/api/v1/retiros/{id}", new { id });
        })
        .WithTags("Retiros")
        .WithSummary("Solicita un retiro")
        .Produces(StatusCodes.Status201Created)
        .RequireAuthorization(PoliticaConClave(Perfiles.PersonalComercio));

        // GET /api/v1/retiros → retiros del comercio.
        v1.MapGet("/retiros", async (
            ConsultarRetirosHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.ListarAsync(ct)))
        .WithTags("Retiros")
        .WithSummary("Lista los retiros")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        // GET /api/v1/retiros/{id} → detalle de un retiro.
        v1.MapGet("/retiros/{retiroId:guid}", async (
            Guid retiroId,
            ConsultarRetirosHandler handler,
            CancellationToken ct) =>
        {
            var retiro = await handler.ObtenerAsync(retiroId, ct);

            return retiro is null ? Results.NotFound() : Results.Ok(retiro);
        })
        .WithTags("Retiros")
        .WithSummary("Consulta el detalle de un retiro")
        .RequireAuthorization(PoliticaConClave(Perfiles.OperadorYComercio));

        return app;
    }

    /// <summary>Solo con clave de API (sin cookie) y con alguno de los perfiles indicados.</summary>
    private static Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> PoliticaConClave(
        string[] perfiles) =>
        politica => politica
            .AddAuthenticationSchemes(ClaveApiAuthenticationHandler.Esquema)
            .RequireRole(perfiles);
}