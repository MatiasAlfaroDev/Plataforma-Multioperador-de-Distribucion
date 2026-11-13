
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Identidad;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Application.Seguridad;

namespace UltimaMilla.Api.Endpoints;

public static class AutenticacionEndpoints
{
    private const string MembresiaClaim = "membresia_id";
    private const string OperadorClaim = "operador_id";
    private const string PerfilClaim = "perfil";
    private const string CuentaComercialClaim = "cuenta_comercial_id";
    private const string ComercioClaim = "comercio_id";
    private const string PerfilComercioClaim = "perfil_comercio";
    private const string AplicacionClaim = "aplicacion";

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
        string.IsNullOrWhiteSpace(request.Password) ||
        string.IsNullOrWhiteSpace(request.ConfirmarPassword))
    {
        return Results.BadRequest(new
        {
            mensaje = "Todos los campos son obligatorios."
        });
    }
    if (request.Password != request.ConfirmarPassword)
{
    return Results.BadRequest(new
    {
        mensaje = "Las contraseñas no coinciden."
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
        string.IsNullOrWhiteSpace(request.ConfirmarPassword) ||
        string.IsNullOrWhiteSpace(request.NombreComercio) ||
        string.IsNullOrWhiteSpace(request.Rut))
    {
        return Results.BadRequest(new
        {
            mensaje = "Todos los campos son obligatorios."
        });
    }

   if (request.Password != request.ConfirmarPassword)
{
    return Results.BadRequest(new
    {
        mensaje = "Las contraseñas no coinciden."
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
        Perfil = Perfiles.AdministradorComercio,        
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

grupo.MapPost("/registro-operador", async (
    RegistroOperadorRequest request,
    UserManager<Usuario> userManager,
    AppDbContext db) =>
{
    // 1. Validar campos obligatorios
    if (string.IsNullOrWhiteSpace(request.NombreCompleto) ||
        string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password) ||
        string.IsNullOrWhiteSpace(request.ConfirmarPassword) ||
        string.IsNullOrWhiteSpace(request.NombreOperador) ||
        string.IsNullOrWhiteSpace(request.ColorPrimario) ||
        string.IsNullOrWhiteSpace(request.EmailContacto))
    {
        return Results.BadRequest(new
        {
            mensaje = "Todos los campos son obligatorios."
        });
    }

    if (request.Password != request.ConfirmarPassword)
{
    return Results.BadRequest(new
    {
        mensaje = "Las contraseñas no coinciden."
    });
}
    var nombre = request.NombreCompleto.Trim();
    var email = request.Email.Trim();
    var nombreOperador = request.NombreOperador.Trim();
    var colorPrimario = request.ColorPrimario.Trim();
    var emailContacto = request.EmailContacto.Trim();

    if (nombre.Length > 150)
    {
        return Results.BadRequest(new
        {
            mensaje = "El nombre no puede superar los 150 caracteres."
        });
    }

    // 2. Verificar que el usuario no exista
    if (await userManager.FindByEmailAsync(email) is not null)
    {
        return Results.Conflict(new
        {
            mensaje = "Ya existe una cuenta con ese correo electrónico."
        });
    }

    // 3. Iniciar transacción
    await using var transaccion =
        await db.Database.BeginTransactionAsync();

    // 4. Crear usuario global
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

    // 5. Crear operador logístico
    var operador = Operador.Crear(
        Guid.NewGuid(),
        nombreOperador,
        colorPrimario,
        emailContacto);

    db.Operadores.Add(operador);

    // 6. Vincular usuario y operador
    var membresia = new MembresiaUsuario
    {
        UsuarioId = usuario.Id,
        OperadorId = operador.Id,
        CuentaComercialId = null,
        Perfil = Perfiles.AdministradorOperador,
        Activa = true
    };

    db.MembresiasUsuarios.Add(membresia);

    // 7. Guardar todo
    await db.SaveChangesAsync();
    await transaccion.CommitAsync();

    return Results.Created("/api/auth/registro-operador", new
    {
        usuarioId = usuario.Id,
        operadorId = operador.Id,
        membresiaId = membresia.Id,
        nombreOperador = operador.Nombre,
        perfil = membresia.Perfil,
        mensaje = "Operador y administrador registrados correctamente."
    });
})
.AllowAnonymous();

        grupo.MapPost("/login", async (
            LoginRequest request,
            UserManager<Usuario> userManager,
            AppDbContext db,
            HttpContext httpContext) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Aplicacion))
            {
                return Results.BadRequest(new
                {
                    mensaje = "Todos los campos son obligatorios."
                });
            }

            var usuario = await userManager.FindByEmailAsync(request.Email.Trim());
            if (usuario is null || !usuario.Activo)
                return Results.Unauthorized();

            var passwordValida = await userManager.CheckPasswordAsync(usuario, request.Password);
            if (!passwordValida)
                return Results.Unauthorized();

            var aplicacion = request.Aplicacion.Trim().ToLowerInvariant();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Name, usuario.UserName ?? usuario.Email ?? string.Empty)
            };

            var requiereSeleccion = false;
            // Recuerda la aplicación para que /seleccionar-operador no entregue un perfil ajeno a ella.
            claims.Add(new Claim(AplicacionClaim, aplicacion));

            if (aplicacion == "portal")
            {
                var comercios = await db.UsuariosComercios
                    .AsNoTracking()
                    .Where(uc =>
                        uc.UsuarioId == usuario.Id &&
                        uc.Activa &&
                        (uc.Perfil == Perfiles.AdministradorComercio ||
                         uc.Perfil == Perfiles.Comercio))
                    .ToListAsync();

                if (comercios.Count == 0)
                {
                    return Results.Json(new
                    {
                        mensaje = "No tenés permisos para acceder al Portal de comercios."
                    }, statusCode: StatusCodes.Status403Forbidden);
                }

                if (comercios.Count == 1)
                {
                    var comercio = comercios[0];
                    claims.Add(new Claim(ComercioClaim, comercio.ComercioId.ToString()));
                    claims.Add(new Claim(PerfilComercioClaim, comercio.Perfil));
                    claims.Add(new Claim(ClaimTypes.Role, comercio.Perfil));
                }
                else
                {
                    requiereSeleccion = true;
                }
            }
            else if (aplicacion == "backoffice")
            {
                var membresias = await db.MembresiasUsuarios
                    .AsNoTracking()
                    .Where(m =>
                        m.UsuarioId == usuario.Id &&
                        m.Activa &&
                        // El administrador de plataforma entra solo a Mensajes fallidos (RNF 6.8).
                        Perfiles.Backoffice.Contains(m.Perfil))
                    .ToListAsync();

                if (membresias.Count == 0)
                {
                    return Results.Json(new
                    {
                        mensaje = "No tenés permisos para acceder al Backoffice."
                    }, statusCode: StatusCodes.Status403Forbidden);
                }

                if (membresias.Count == 1)
                {
                    var membresia = membresias[0];
                    claims.Add(new Claim(MembresiaClaim, membresia.Id.ToString()));
                    claims.Add(new Claim(OperadorClaim, membresia.OperadorId.ToString()));
                    claims.Add(new Claim(PerfilClaim, membresia.Perfil));
                    claims.Add(new Claim(ClaimTypes.Role, membresia.Perfil));

                    if (membresia.CuentaComercialId.HasValue)
                    {
                        claims.Add(new Claim(
                            CuentaComercialClaim,
                            membresia.CuentaComercialId.Value.ToString()));
                    }
                }
                else
                {
                    requiereSeleccion = true;
                }
            }
            else if (aplicacion == "movil")
            {
                // La app del repartidor elige el operador después, con /seleccionar-operador.
                var esRepartidor = await db.MembresiasUsuarios
                    .AsNoTracking()
                    .AnyAsync(m =>
                        m.UsuarioId == usuario.Id &&
                        m.Activa &&
                        m.Perfil == Perfiles.Repartidor);

                if (!esRepartidor)
                {
                    return Results.Json(new
                    {
                        mensaje = "No tenés permisos para acceder a la app de repartidores."
                    }, statusCode: StatusCodes.Status403Forbidden);
                }

                requiereSeleccion = true;
            }
            else
            {
                return Results.BadRequest(new
                {
                    mensaje = "Aplicación no válida."
                });
            }

            await IniciarSesionAsync(httpContext, claims);

            return Results.Ok(new
            {
                usuario.Id,
                usuario.Email,
                usuario.NombreCompleto,
                requiereSeleccion,
                comercioId = claims.FirstOrDefault(c => c.Type == ComercioClaim)?.Value,
                operadorId = claims.FirstOrDefault(c => c.Type == OperadorClaim)?.Value
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

            if (membresia is null || !PerfilPermitidoEnAplicacion(context, membresia.Perfil))
                return Results.Forbid();

            var claims = new List<Claim>
            {
                new(AplicacionClaim, context.User.FindFirstValue(AplicacionClaim) ?? string.Empty),
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
        new(PerfilComercioClaim, vinculacion.Perfil),
        // Sin el rol, los endpoints con RequireRole devolvían 403 tras elegir el comercio.
        new(ClaimTypes.Role, vinculacion.Perfil),
        new(AplicacionClaim, "portal")
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

    // Una sesión iniciada en el Backoffice no puede tomar una membresía de repartidor, ni al revés.
    private static bool PerfilPermitidoEnAplicacion(HttpContext context, string perfil) =>
        context.User.FindFirstValue(AplicacionClaim) switch
        {
            "backoffice" => Perfiles.Backoffice.Contains(perfil),
            "movil" => perfil == Perfiles.Repartidor,
            "portal" => false,
            // ponytail: cookies emitidas antes de este claim; vencen solas.
            _ => true
        };

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
    string Password,
    string ConfirmarPassword
);

public sealed record RegistroComercioRequest(
    string NombreCompleto,
    string Email,
    string Password,
    string ConfirmarPassword,
    string NombreComercio,
    string Rut
);

public sealed record RegistroOperadorRequest(
    string NombreCompleto,
    string Email,
    string Password,
    string ConfirmarPassword,
    string NombreOperador,
    string ColorPrimario,
    string EmailContacto
);
public sealed record LoginRequest(
    string Email,
    string Password,
    string Aplicacion
);
public sealed record SeleccionarOperadorRequest(Guid MembresiaId);

public sealed record SeleccionarComercioRequest(Guid ComercioId);
