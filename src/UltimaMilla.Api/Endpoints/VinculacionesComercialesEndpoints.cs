using Microsoft.AspNetCore.Antiforgery;
using UltimaMilla.Application.CuentasComerciales.Commands.SolicitarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Queries.ListarSolicitudesPendientes;
using UltimaMilla.Application.CuentasComerciales.Commands.AceptarVinculacion;
using UltimaMilla.Application.CuentasComerciales.Commands.RechazarVinculacion;

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

        return app;
    }
}