using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Abstractions.Caching;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Plataforma;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Plataforma;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Banderas de funcionalidad (aclaración 3.2). Las activa el administrador de la plataforma, operador por
/// operador, y cada cambio queda registrado con quién y cuándo. Como Personal, vive junto a la API porque
/// es administración de la plataforma y no un caso de uso de un operador.
/// </summary>
public static class BanderasEndpoints
{
    public static IEndpointRouteBuilder MapBanderasEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/banderas")
            .WithTags("Banderas")
            .RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorPlataforma));

        // Estado de cada bandera en cada operador, más el historial reciente.
        admin.MapGet("", async (AppDbContext db, CancellationToken ct) =>
        {
            var operadores = await db.Operadores.IgnoreQueryFilters().AsNoTracking()
                .OrderBy(o => o.Nombre)
                .Select(o => new { o.Id, o.Nombre })
                .ToListAsync(ct);

            var filas = await db.BanderasFuncionalidad.AsNoTracking().ToListAsync(ct);

            var estado = operadores.Select(o => new
            {
                OperadorId = o.Id,
                Operador = o.Nombre,
                Banderas = Banderas.Todas.ToDictionary(
                    b => b,
                    b => filas.FirstOrDefault(f => f.OperadorId == o.Id && f.Nombre == b)?.Habilitada ?? true)
            });

            var historial = await db.CambiosBandera.AsNoTracking()
                .OrderByDescending(c => c.ModificadoEn)
                .Take(50)
                .ToListAsync(ct);

            return Results.Ok(new { estado, historial });
        });

        admin.MapPut("/{operadorId:guid}/{bandera}", async (
            Guid operadorId,
            string bandera,
            CambiarBanderaRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            AppDbContext db,
            ICacheService cache,
            TimeProvider reloj,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Token de seguridad inválido");

            if (!Banderas.Todas.Contains(bandera))
                throw new ArgumentException($"Bandera desconocida. Opciones: {string.Join(", ", Banderas.Todas)}.");

            var fila = await db.BanderasFuncionalidad
                .FirstOrDefaultAsync(b => b.OperadorId == operadorId && b.Nombre == bandera, ct);

            if (fila is null)
            {
                fila = BanderaFuncionalidad.Crear(bandera, operadorId);
                db.BanderasFuncionalidad.Add(fila);
            }

            var quien = context.User.FindFirstValue(ClaimTypes.Name) ?? "desconocido";
            db.CambiosBandera.Add(fila.Cambiar(request.Habilitada, quien, reloj.GetUtcNow()));
            await db.SaveChangesAsync(ct);

            // Llega a todas las réplicas en la próxima lectura: la entrada es compartida.
            await cache.EliminarAsync(BanderasService.Clave(operadorId), ct);

            return Results.NoContent();
        });

        // Banderas de la sesión, para que las webs y la app muestren u oculten las funcionalidades.
        // El comercio recibe las de cada operador con el que trabaja.
        app.MapGet("/api/banderas", async (
            ITenantContext tenant,
            IBanderas banderas,
            ICuentaComercialRepository cuentas,
            CancellationToken ct) =>
        {
            if (tenant.OperadorId is { } operadorId)
                return Results.Ok(await DeOperadorAsync(banderas, operadorId, ct));

            if (tenant.ComercioId is not { } comercioId)
                return Results.Ok(new Dictionary<string, bool>());

            var porOperador = new Dictionary<Guid, Dictionary<string, bool>>();
            foreach (var cuenta in await cuentas.ListarPorComercioAsync(comercioId, ct))
                porOperador[cuenta.OperadorId] = await DeOperadorAsync(banderas, cuenta.OperadorId, ct);

            return Results.Ok(porOperador);
        })
        .WithTags("Banderas")
        .RequireAuthorization();

        return app;
    }

    private static async Task<Dictionary<string, bool>> DeOperadorAsync(IBanderas banderas, Guid operadorId, CancellationToken ct)
    {
        var resultado = new Dictionary<string, bool>();
        foreach (var bandera in Banderas.Todas)
            resultado[bandera] = await banderas.EstaHabilitadaAsync(bandera, operadorId, ct);
        return resultado;
    }
}

public sealed record CambiarBanderaRequest(bool Habilitada);
