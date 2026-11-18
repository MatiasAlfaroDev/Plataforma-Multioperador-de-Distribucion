using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Api.Seguridad;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Letra 5.1, requerimiento 6: gestión de vehículos del operador, con su capacidad de carga.
/// Misma política que el resto de la configuración del operador.
/// </summary>
public static class VehiculosEndpoints
{
    public static IEndpointRouteBuilder MapVehiculosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/vehiculos")
            .WithTags("Vehiculos")
            .RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));

        grupo.MapGet("", async (ITenantContext tenant, AppDbContext db, CancellationToken ct) =>
        {
            var operadorId = tenant.OperadorId ?? throw new UnauthorizedAccessException();

            var vehiculos = await db.Vehiculos
                .AsNoTracking()
                .Where(v => v.OperadorId == operadorId)
                .OrderByDescending(v => v.Activo)
                .ThenBy(v => v.Matricula)
                .Select(v => new VehiculoItem(
                    v.Id, v.Matricula, v.CapacidadPesoKg, v.CapacidadVolumenM3, v.Activo))
                .ToListAsync(ct);

            return Results.Ok(vehiculos);
        });

        grupo.MapPost("", async (
            AltaVehiculoRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            ITenantContext tenant,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return TokenInvalido();

            var operadorId = tenant.OperadorId ?? throw new UnauthorizedAccessException();

            if (string.IsNullOrWhiteSpace(request.Matricula))
                throw new ArgumentException("La matrícula es obligatoria.");
            if (request.CapacidadPesoKg <= 0)
                throw new ArgumentException("La capacidad de peso debe ser mayor que cero.");
            if (request.CapacidadVolumenM3 <= 0)
                throw new ArgumentException("La capacidad de volumen debe ser mayor que cero.");

            var matricula = request.Matricula.Trim().ToUpperInvariant();
            if (await db.Vehiculos.AnyAsync(v => v.OperadorId == operadorId && v.Matricula == matricula, ct))
                throw new ConflictoException($"Ya existe un vehículo con la matrícula {matricula}.");

            var vehiculo = Vehiculo.Crear(
                operadorId, matricula, request.CapacidadPesoKg, request.CapacidadVolumenM3);
            db.Vehiculos.Add(vehiculo);
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/api/vehiculos/{vehiculo.Id}",
                new VehiculoItem(
                    vehiculo.Id, vehiculo.Matricula, vehiculo.CapacidadPesoKg,
                    vehiculo.CapacidadVolumenM3, vehiculo.Activo));
        });

        // Baja lógica: un vehículo inactivo deja de ofrecerse al armar hojas de ruta, pero las existentes lo conservan.
        grupo.MapPost("/{id:guid}/activo", async (
            Guid id,
            CambiarEstadoVehiculoRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            ITenantContext tenant,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return TokenInvalido();

            var operadorId = tenant.OperadorId ?? throw new UnauthorizedAccessException();

            var vehiculo = await db.Vehiculos
                .FirstOrDefaultAsync(v => v.Id == id && v.OperadorId == operadorId, ct);
            if (vehiculo is null)
                return Results.NotFound();

            if (request.Activo) vehiculo.Activar(); else vehiculo.Desactivar();
            await db.SaveChangesAsync(ct);

            return Results.Ok(new VehiculoItem(
                vehiculo.Id, vehiculo.Matricula, vehiculo.CapacidadPesoKg,
                vehiculo.CapacidadVolumenM3, vehiculo.Activo));
        });

        return app;
    }

    private static IResult TokenInvalido() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Token de seguridad inválido",
        detail: "La solicitud no contiene un token antifalsificación válido.");
}

public sealed record AltaVehiculoRequest(string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3);

public sealed record CambiarEstadoVehiculoRequest(bool Activo);

public sealed record VehiculoItem(
    Guid Id, string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3, bool Activo);
