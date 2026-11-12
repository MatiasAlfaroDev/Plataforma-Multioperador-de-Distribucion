using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using UltimaMilla.Api.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Identidad;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Letra 3.3: el administrador del operador gestiona sus usuarios (despachadores, operarios de depósito,
/// repartidores) y el administrador del comercio gestiona los del comercio. Vive junto a la autenticación
/// porque Usuario y las membresías son de Identity (Infrastructure), igual que el registro.
/// </summary>
public static class PersonalEndpoints
{
    private static readonly string[] PerfilesDelOperador =
        [Perfiles.AdministradorOperador, Perfiles.Despachador, Perfiles.OperarioDeposito, Perfiles.Repartidor];

    public static IEndpointRouteBuilder MapPersonalEndpoints(this IEndpointRouteBuilder app)
    {
        var operador = app.MapGroup("/api/personal")
            .WithTags("Personal")
            .RequireAuthorization(policy => policy.RequireRole(Perfiles.Configuracion));

        operador.MapGet("", async (ITenantContext tenant, AppDbContext db, CancellationToken ct) =>
        {
            var operadorId = tenant.OperadorId ?? throw new UnauthorizedAccessException();

            var personal = await (
                from m in db.MembresiasUsuarios.AsNoTracking()
                join u in db.Users.AsNoTracking() on m.UsuarioId equals u.Id
                where m.OperadorId == operadorId && m.Activa && m.Perfil != Perfiles.AdministradorPlataforma
                orderby m.Perfil, u.NombreCompleto
                select new PersonalItem(m.Id, u.NombreCompleto, u.Email ?? string.Empty, m.Perfil))
                .ToListAsync(ct);

            return Results.Ok(personal);
        });

        operador.MapPost("", async (
            AltaPersonalRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            ITenantContext tenant,
            UserManager<Usuario> userManager,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return TokenInvalido();

            if (!PerfilesDelOperador.Contains(request.Perfil))
                throw new ArgumentException($"Perfil no válido. Opciones: {string.Join(", ", PerfilesDelOperador)}.");

            var operadorId = tenant.OperadorId ?? throw new UnauthorizedAccessException();
            var usuario = await ObtenerOCrearUsuarioAsync(request.NombreCompleto, request.Email, request.Password, userManager);

            if (await db.MembresiasUsuarios.AnyAsync(m =>
                    m.UsuarioId == usuario.Id && m.OperadorId == operadorId && m.Perfil == request.Perfil, ct))
                throw new ConflictoException("El usuario ya tiene ese perfil en el operador.");

            db.MembresiasUsuarios.Add(new MembresiaUsuario
            {
                UsuarioId = usuario.Id,
                OperadorId = operadorId,
                Perfil = request.Perfil,
                Activa = true
            });

            // Un repartidor con cuenta queda vinculado a su ficha para que la app le muestre sus hojas de ruta.
            if (request.Perfil == Perfiles.Repartidor)
            {
                var repartidor = Repartidor.Crear(operadorId, usuario.NombreCompleto, request.Telefono ?? string.Empty);
                repartidor.VincularUsuario(usuario.Id);
                db.Repartidores.Add(repartidor);
            }

            await db.SaveChangesAsync(ct);
            return Results.Created("/api/personal", new { usuario.Id, request.Perfil });
        });

        var comercio = app.MapGroup("/api/comercio/usuarios")
            .WithTags("Personal")
            .RequireAuthorization(policy => policy.RequireRole(Perfiles.AdministradorComercio));

        comercio.MapGet("", async (ITenantContext tenant, AppDbContext db, CancellationToken ct) =>
        {
            var comercioId = tenant.ComercioId ?? throw new UnauthorizedAccessException();

            var usuarios = await (
                from uc in db.UsuariosComercios.AsNoTracking()
                join u in db.Users.AsNoTracking() on uc.UsuarioId equals u.Id
                where uc.ComercioId == comercioId && uc.Activa
                orderby uc.Perfil, u.NombreCompleto
                select new PersonalItem(uc.Id, u.NombreCompleto, u.Email ?? string.Empty, uc.Perfil))
                .ToListAsync(ct);

            return Results.Ok(usuarios);
        });

        comercio.MapPost("", async (
            AltaPersonalRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            ITenantContext tenant,
            UserManager<Usuario> userManager,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return TokenInvalido();

            if (request.Perfil is not (Perfiles.Comercio or Perfiles.AdministradorComercio))
                throw new ArgumentException("Perfil no válido. Opciones: Comercio, AdministradorComercio.");

            var comercioId = tenant.ComercioId ?? throw new UnauthorizedAccessException();
            var usuario = await ObtenerOCrearUsuarioAsync(request.NombreCompleto, request.Email, request.Password, userManager);

            if (await db.UsuariosComercios.AnyAsync(uc => uc.UsuarioId == usuario.Id && uc.ComercioId == comercioId, ct))
                throw new ConflictoException("El usuario ya pertenece al comercio.");

            db.UsuariosComercios.Add(new UsuarioComercio
            {
                UsuarioId = usuario.Id,
                ComercioId = comercioId,
                Perfil = request.Perfil,
                Activa = true
            });

            await db.SaveChangesAsync(ct);
            return Results.Created("/api/comercio/usuarios", new { usuario.Id, request.Perfil });
        });

        // RF 37: clave de API del usuario para que el sistema del comercio cree envíos y retiros sin navegador.
        // Se muestra una sola vez; generar otra invalida la anterior.
        comercio.MapPost("/clave-api", async (
            HttpContext context,
            IAntiforgery antiforgery,
            ITenantContext tenant,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return TokenInvalido();

            var comercioId = tenant.ComercioId ?? throw new UnauthorizedAccessException();
            var usuarioId = tenant.UsuarioId ?? throw new UnauthorizedAccessException();

            var vinculo = await db.UsuariosComercios
                .FirstOrDefaultAsync(uc => uc.UsuarioId == usuarioId && uc.ComercioId == comercioId && uc.Activa, ct)
                ?? throw new UnauthorizedAccessException();

            var clave = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            vinculo.ClaveApiHash = ClaveApiAuthenticationHandler.Hash(clave);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { clave, encabezado = ClaveApiAuthenticationHandler.Encabezado });
        });

        return app;
    }

    // Una misma persona puede trabajar para varios operadores o comercios: si ya existe, se reutiliza
    // su cuenta (y su contraseña) y solo se le agrega el vínculo.
    private static async Task<Usuario> ObtenerOCrearUsuarioAsync(
        string nombreCompleto, string email, string? password, UserManager<Usuario> userManager)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email es obligatorio.");

        var existente = await userManager.FindByEmailAsync(email.Trim());
        if (existente is not null)
            return existente;

        if (string.IsNullOrWhiteSpace(nombreCompleto) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Nombre y contraseña son obligatorios para un usuario nuevo.");

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email.Trim(),
            Email = email.Trim(),
            NombreCompleto = nombreCompleto.Trim(),
            Activo = true,
            EmailConfirmed = true
        };

        var resultado = await userManager.CreateAsync(usuario, password);
        if (!resultado.Succeeded)
            throw new ArgumentException(string.Join(" ", resultado.Errors.Select(e => e.Description)));

        return usuario;
    }

    private static IResult TokenInvalido() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Token de seguridad inválido",
        detail: "La solicitud no contiene un token antifalsificación válido.");
}

public sealed record AltaPersonalRequest(
    string NombreCompleto,
    string Email,
    string? Password,
    string Perfil,
    string? Telefono);

public sealed record PersonalItem(Guid Id, string Nombre, string Email, string Perfil);
