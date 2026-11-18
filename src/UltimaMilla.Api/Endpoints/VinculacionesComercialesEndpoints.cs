using Microsoft.AspNetCore.Antiforgery;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.CuentasComerciales.Commands.SolicitarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;
using UltimaMilla.Application.CuentasComerciales.Commands.AceptarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Commands.RechazarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Commands.ConfigurarDireccionRecepcion;
using UltimaMilla.Application.CuentasComerciales.Queries.ObtenerDireccionRecepcion;
using UltimaMilla.Application.CuentasComerciales.Commands.ModificarComercio;
using UltimaMilla.Application.CuentasComerciales.Commands.CambiarEstadoCuentaComercial;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Api.Endpoints;

public static class VinculacionesComercialesEndpoints
{
    public static IEndpointRouteBuilder MapVinculacionesComercialesEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/vinculaciones")
            .RequireAuthorization();

        grupo.MapPost("/solicitar", async (
            SolicitarVinculacionCommand command,
            SolicitarVinculacionHandler handler,
            HttpContext context,
            IAntiforgery antiforgery,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Token de seguridad inválido.");
            }

            try
            {
                var id = await handler.Handle(command, ct);

                return Results.Created(
                    $"/api/vinculaciones/{id}",
                    new
                    {
                        Id = id,
                        Estado = "Pendiente",
                        Mensaje = "Solicitud enviada correctamente."
                    });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { mensaje = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { mensaje = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { mensaje = ex.Message });
            }
        });

     grupo.MapGet("/pendientes", async (
            ListarSolicitudesPendientesHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var solicitudes = await handler.Handle(
                    new ListarSolicitudesPendientesQuery(),
                    ct);

                return Results.Ok(solicitudes);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });

            grupo.MapPost("/{id:guid}/aceptar", async (
            Guid id,
            AceptarVinculacionHandler handler,
            HttpContext context,
            IAntiforgery antiforgery,
            CancellationToken ct) =>
        {
    if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido.");
        }

        try
        {
            await handler.Handle(
                new AceptarVinculacionCommand(id),
                ct);

            return Results.Ok(new
            {
                Mensaje = "Vinculación aceptada correctamente.",
                Estado = "Confirmada"
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { mensaje = ex.Message });
        }
    });


    grupo.MapPost("/{id:guid}/rechazar", async (
    Guid id,
    RechazarVinculacionHandler handler,
    HttpContext context,
    IAntiforgery antiforgery,
    CancellationToken ct) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido.");
        }

        try
        {
            await handler.Handle(
                new RechazarVinculacionCommand(id),
                ct);

            return Results.Ok(new
            {
                Mensaje = "Vinculación rechazada correctamente.",
                Estado = "Rechazada"
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { mensaje = ex.Message });
        }
    });


    grupo.MapPut("/{id:guid}/direccion-recepcion", async (
        Guid id,
        ConfigurarDireccionRecepcionRequest request,
        ConfigurarDireccionRecepcionHandler handler,
        HttpContext context,
        IAntiforgery antiforgery,
        CancellationToken ct) =>
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Token de seguridad inválido.");
        }

        try
        {
            await handler.Handle(
                new ConfigurarDireccionRecepcionCommand(
                    id,
                    request.Calle,
                    request.Numero,
                    request.Localidad,
                    request.Departamento),
                ct);

            return Results.Ok(new
            {
                Mensaje = "Dirección de recepción actualizada correctamente."
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (NoEncontradoException ex)
        {
            return Results.NotFound(new { mensaje = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { mensaje = ex.Message });
        }
    });


    grupo.MapGet("/{id:guid}/direccion-recepcion", async (
        Guid id,
        ObtenerDireccionRecepcionHandler handler,
        CancellationToken ct) =>
    {
        try
        {
            var direccion = await handler.Handle(
                new ObtenerDireccionRecepcionQuery(id),
                ct);

            return Results.Ok(direccion);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (NoEncontradoException ex)
        {
            return Results.NotFound(new { mensaje = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { mensaje = ex.Message });
        }
    });

        // RF 1: baja lógica y reactivación de un comercio aceptado, por el mismo actor que acepta
        // las solicitudes. Los errores los traduce ManejadorDeErrores.
        grupo.MapPut("/{id:guid}/estado", async (
            Guid id,
            CambiarEstadoCuentaComercialRequest request,
            CambiarEstadoCuentaComercialHandler handler,
            HttpContext context,
            IAntiforgery antiforgery,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Token de seguridad inválido.");

            await handler.Handle(new CambiarEstadoCuentaComercialCommand(id, request.Activa), ct);
            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole(Perfiles.Configuracion));

        // RF 1: el administrador del comercio modifica su propio comercio (el de la sesión).
        // El operador no puede: el comercio es global y lo ven todos sus operadores.
        app.MapGroup("/api/comercio")
            .RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorComercio))
            .MapPut("/datos", async (
                ModificarComercioRequest request,
                ModificarComercioHandler handler,
                HttpContext context,
                IAntiforgery antiforgery,
                CancellationToken ct) =>
            {
                if (!await antiforgery.IsRequestValidAsync(context))
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Token de seguridad inválido.");

                await handler.Handle(new ModificarComercioCommand(request.Nombre), ct);
                return Results.NoContent();
            });

        return app;
    }
}

public sealed record ModificarComercioRequest(string Nombre);

public sealed record CambiarEstadoCuentaComercialRequest(bool Activa);

public sealed record ConfigurarDireccionRecepcionRequest(
    string Calle,
    string Numero,
    string Localidad,
    string Departamento);