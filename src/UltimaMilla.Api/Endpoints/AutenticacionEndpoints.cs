
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Identidad;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Api.Endpoints;

public static class AutenticacionEndpoints
{
    private const string MembresiaClaim = "membresia_id";
    private const string OperadorClaim = "operador_id";
    private const string PerfilClaim = "perfil";
    private const string CuentaComercialClaim = "cuenta_comercial_id";
    private const string ComercioClaim = "comercio_id";
    private const string PerfilComercioClaim = "perfil_comercio";

    public static RouteGroupBuilder MapAutenticacionEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth")
            .WithTags("Autenticación");


        grupo.MapPost("/registro", async (
    RegistroUsuarioRequest request,
    UserManager<Usuario> userManager) =>
{
    // Validar los datos ingresados.
    if (string.IsNullOrWhiteSpace(request.NombreCompleto) ||
        string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new
        {
            mensaje = "Todos los campos son obligatorios."
        });
    }

    var nombre = request.NombreCompleto.Trim();
    var email = request.Email.Trim();

    if (nombre.Length > 150)
    {
        return Results.BadRequest(new
        {
            mensaje = "El nombre no puede superar los 150 caracteres."
        });
    }

    // Verificar si el correo ya está registrado.
    var usuarioExistente = await userManager.FindByEmailAsync(email);

    if (usuarioExistente is not null)
    {
        return Results.Conflict(new
        {
            mensaje = "Ya existe una cuenta con ese correo electrónico."
        });
    }

    // Crear el usuario global, sin asignarle ningún operador.
    var usuario = new Usuario
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        NombreCompleto = nombre,
        Activo = true
    };

    var resultado = await userManager.CreateAsync(
        usuario,
        request.Password);

    if (!resultado.Succeeded)
    {
        return Results.BadRequest(new
        {
            mensaje = "No se pudo completar el registro.",
            errores = resultado.Errors.Select(e => e.Description)
        });
    }

    return Results.Created("/api/auth/registro", new
    {
        usuario.Id,
        usuario.NombreCompleto,
        usuario.Email,
        mensaje = "Usuario registrado correctamente."
    });
})
.AllowAnonymous();

grupo.MapPost("/registro-comercio", async (
    RegistroComercioRequest request,
    UserManager<Usuario> userManager,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.NombreCompleto) ||
        string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password) ||
        string.IsNullOrWhiteSpace(request.NombreComercio) ||
        string.IsNullOrWhiteSpace(request.Rut))
    {
        return Results.BadRequest(new
        {
            mensaje = "Todos los campos son obligatorios."
        });
    }

    var nombre = request.NombreCompleto.Trim();
    var email = request.Email.Trim();
    var nombreComercio = request.NombreComercio.Trim();
    var rut = request.Rut.Trim();

    if (nombre.Length > 150)
    {
        return Results.BadRequest(new
        {
            mensaje = "El nombre no puede superar los 150 caracteres."
        });
    }

    if (await userManager.FindByEmailAsync(email) is not null)
    {
        return Results.Conflict(new
        {
            mensaje = "Ya existe una cuenta con ese correo electrónico."
        });
    }

    if (await db.Set<Comercio>()
        .AnyAsync(c => c.Rut == rut))
    {
        return Results.Conflict(new
        {
            mensaje = "Ya existe un comercio registrado con ese RUT."
        });
    }

    await using var transaccion =
        await db.Database.BeginTransactionAsync();

    var usuario = new Usuario
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        NombreCompleto = nombre,
        Activo = true
    };

    var resultado = await userManager.CreateAsync(
        usuario,
        request.Password);

    if (!resultado.Succeeded)
    {
        await transaccion.RollbackAsync();

        return Results.BadRequest(new
        {
            mensaje = "No se pudo crear el usuario.",
            errores = resultado.Errors.Select(e => e.Description)
        });
    }

    var comercio = Comercio.Crear(
        Guid.NewGuid(),
        rut,
        nombreComercio);

    db.Set<Comercio>().Add(comercio);

    var usuarioComercio = new UsuarioComercio
    {
        UsuarioId = usuario.Id,
        ComercioId = comercio.Id,
        Perfil = "Administrador",
        Activa = true
    };

    db.Set<UsuarioComercio>().Add(usuarioComercio);

    await db.SaveChangesAsync();
    await transaccion.CommitAsync();

    return Results.Created("/api/auth/registro-comercio", new
    {
        usuarioId = usuario.Id,
        comercioId = comercio.Id,
        nombreComercio = comercio.Nombre,
        perfil = usuarioComercio.Perfil,
        mensaje = "Comercio y administrador registrados correctamente."
    });
})
.AllowAnonymous();

        grupo.MapPost("/login", async (
            LoginRequest request,
            UserManager<Usuario> userManager,
            HttpContext httpContext) =>
        {
            var usuario = await userManager.FindByEmailAsync(request.Email);

            if (usuario is null || !usuario.Activo)
                return Results.Unauthorized();

            var passwordValida = await userManager.CheckPasswordAsync(
                usuario,
                request.Password);

            if (!passwordValida)
                return Results.Unauthorized();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(
                    ClaimTypes.Name,
                    usuario.UserName ?? usuario.Email ?? string.Empty)
            };

            await IniciarSesionAsync(httpContext, claims);

            return Results.Ok(new
            {
                usuario.Id,
                usuario.Email,
                usuario.NombreCompleto
            });
        });

        grupo.MapGet("/membresias", async (
            HttpContext context,
            AppDbContext db) =>
        {
            if (!ObtenerUsuarioId(context, out var usuarioId))
                return Results.Unauthorized();

            var membresias = await db.Set<MembresiaUsuario>()
                .AsNoTracking()
                .Where(m => m.UsuarioId == usuarioId && m.Activa)
                .Select(m => new
                {
                    m.Id,
                    m.OperadorId,
                    m.CuentaComercialId,
                    m.Perfil
                })
                .ToListAsync();

            return Results.Ok(membresias);
        }).RequireAuthorization();

        grupo.MapPost("/seleccionar-operador", async (
            SeleccionarOperadorRequest request,
            HttpContext context,
            AppDbContext db,
            UserManager<Usuario> userManager) =>
        {
            if (!ObtenerUsuarioId(context, out var usuarioId))
                return Results.Unauthorized();

            var usuario = await userManager.FindByIdAsync(
                usuarioId.ToString());

            if (usuario is null || !usuario.Activo)
                return Results.Unauthorized();

            var membresia = await db.Set<MembresiaUsuario>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.Id == request.MembresiaId &&
                    m.UsuarioId == usuarioId &&
                    m.Activa);

            if (membresia is null)
                return Results.Forbid();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(
                    ClaimTypes.Name,
                    usuario.UserName ?? usuario.Email ?? string.Empty),
                new(MembresiaClaim, membresia.Id.ToString()),
                new(OperadorClaim, membresia.OperadorId.ToString()),
                new(PerfilClaim, membresia.Perfil),
                new(ClaimTypes.Role, membresia.Perfil)
            };

            if (membresia.CuentaComercialId.HasValue)
            {
                claims.Add(new Claim(
                    CuentaComercialClaim,
                    membresia.CuentaComercialId.Value.ToString()));
            }

            await IniciarSesionAsync(context, claims);

            return Results.Ok(new
            {
                membresia.Id,
                membresia.OperadorId,
                membresia.CuentaComercialId,
                membresia.Perfil
            });
        }).RequireAuthorization();

        grupo.MapGet("/mis-comercios", async (
    HttpContext context,
    AppDbContext db) =>
{
    if (!ObtenerUsuarioId(context, out var usuarioId))
        return Results.Unauthorized();

    var comercios = await (
        from uc in db.UsuariosComercios.AsNoTracking()
        join c in db.Set<Comercio>().AsNoTracking()
            on uc.ComercioId equals c.Id
        where uc.UsuarioId == usuarioId && uc.Activa
        select new
        {
            c.Id,
            c.Nombre,
            c.Rut,
            uc.Perfil
        }
    ).ToListAsync();

    return Results.Ok(comercios);
}).RequireAuthorization();

grupo.MapPost("/seleccionar-comercio", async (
    SeleccionarComercioRequest request,
    HttpContext context,
    AppDbContext db,
    UserManager<Usuario> userManager) =>
{
    if (!ObtenerUsuarioId(context, out var usuarioId))
        return Results.Unauthorized();

    var usuario = await userManager.FindByIdAsync(
        usuarioId.ToString());

    if (usuario is null || !usuario.Activo)
        return Results.Unauthorized();

    var vinculacion = await db.UsuariosComercios
        .AsNoTracking()
        .FirstOrDefaultAsync(uc =>
            uc.UsuarioId == usuarioId &&
            uc.ComercioId == request.ComercioId &&
            uc.Activa);

    if (vinculacion is null)
        return Results.Forbid();

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new(
            ClaimTypes.Name,
            usuario.UserName ?? usuario.Email ?? string.Empty),
        new(ComercioClaim, vinculacion.ComercioId.ToString()),
        new(PerfilComercioClaim, vinculacion.Perfil)
    };

    await IniciarSesionAsync(context, claims);

    return Results.Ok(new
    {
        vinculacion.ComercioId,
        vinculacion.Perfil,
        mensaje = "Comercio seleccionado correctamente."
    });
}).RequireAuthorization();

        grupo.MapGet("/me", (HttpContext context) =>
        {
            return Results.Ok(new
            {
                Autenticado = context.User.Identity?.IsAuthenticated ?? false,
                UsuarioId = context.User.FindFirstValue(
                    ClaimTypes.NameIdentifier),
                MembresiaId = context.User.FindFirstValue(MembresiaClaim),
                OperadorId = context.User.FindFirstValue(OperadorClaim),
                CuentaComercialId = context.User.FindFirstValue(
                    CuentaComercialClaim),
                Perfil = context.User.FindFirstValue(PerfilClaim),
                ComercioId = context.User.FindFirstValue(ComercioClaim),
                PerfilComercio = context.User.FindFirstValue(PerfilComercioClaim)   
            });
        }).RequireAuthorization();

        grupo.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(
                IdentityConstants.ApplicationScheme);

            return Results.NoContent();
        }).RequireAuthorization();

        return grupo;
    }

    private static bool ObtenerUsuarioId(
        HttpContext context,
        out Guid usuarioId)
    {
        return Guid.TryParse(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out usuarioId);
    }

    private static Task IniciarSesionAsync(
        HttpContext context,
        IEnumerable<Claim> claims)
    {
        var identidad = new ClaimsIdentity(
            claims,
            IdentityConstants.ApplicationScheme);

        var principal = new ClaimsPrincipal(identidad);

        return context.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal);
    }
}

public sealed record RegistroUsuarioRequest(
    string NombreCompleto,
    string Email,
    string Password);

    public sealed record RegistroComercioRequest(
    string NombreCompleto,
    string Email,
    string Password,
    string NombreComercio,
    string Rut);

public sealed record LoginRequest(string Email, string Password);

public sealed record SeleccionarOperadorRequest(Guid MembresiaId);

public sealed record SeleccionarComercioRequest(Guid ComercioId);
