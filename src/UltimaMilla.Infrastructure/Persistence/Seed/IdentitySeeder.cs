using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Identity;

namespace UltimaMilla.Infrastructure.Persistence.Seed;

public static class IdentitySeeder
{
    public static async Task SembrarAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        const string email = "admin@rapienvios.uy";

        var usuario = await userManager.FindByEmailAsync(email);

        if (usuario is null)
        {
            usuario = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                NombreCompleto = "Administrador RapiEnvíos",
                Activo = true
            };

            var resultado = await userManager.CreateAsync(
                usuario,
                "Admin1234!");

            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ",
                        resultado.Errors.Select(e => e.Description)));
            }
        }

        bool existeRelacion = await db.UsuariosOperadores
            .AnyAsync(x =>
                x.UsuarioId == usuario.Id &&
                x.OperadorId == DbSeeder.RapiEnviosId);

        if (!existeRelacion)
        {
            db.UsuariosOperadores.Add(new UsuarioOperador
            {
                UsuarioId = usuario.Id,
                OperadorId = DbSeeder.RapiEnviosId,
                Rol = "ADMINISTRADOR"
            });

            await db.SaveChangesAsync();
        }
    }

    public static async Task SembrarComercioPruebaAsync(
    UserManager<ApplicationUser> userManager,
    AppDbContext db)
{
    const string email = "admin@tiendanorte.test";
    const string password = "TiendaTest123!";

    var comercioId = Guid.Parse(
        "22222222-0000-0000-0000-000000000001");

    var usuario = await userManager.FindByEmailAsync(email);

    if (usuario is null)
    {
        usuario = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = "Administrador Tienda Norte",
            Activo = true
        };

        var resultado = await userManager.CreateAsync(
            usuario,
            password);

        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ",
                    resultado.Errors.Select(e => e.Description)));
        }
    }

    bool existeRelacion = await db.UsuariosComercios
        .AnyAsync(x =>
            x.UsuarioId == usuario.Id &&
            x.ComercioId == comercioId);

    if (!existeRelacion)
    {
        db.UsuariosComercios.Add(new UsuarioComercio
        {
            UsuarioId = usuario.Id,
            ComercioId = comercioId,
            Rol = "ADMINISTRADOR"
        });

        await db.SaveChangesAsync();
    }
}
public static async Task SembrarModaExpressPruebaAsync(
    UserManager<ApplicationUser> userManager,
    AppDbContext db)
{
    const string email = "admin@modaexpress.test";
    const string password = "ModaTest123!";

    var comercioId = Guid.Parse(
        "22222222-0000-0000-0000-000000000002");

    var usuario = await userManager.FindByEmailAsync(email);

    if (usuario is null)
    {
        usuario = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = "Administrador Moda Express",
            Activo = true
        };

        var resultado = await userManager.CreateAsync(
            usuario,
            password);

        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ",
                    resultado.Errors.Select(e => e.Description)));
        }
    }

    bool existeRelacion = await db.UsuariosComercios
        .AnyAsync(x =>
            x.UsuarioId == usuario.Id &&
            x.ComercioId == comercioId);

    if (!existeRelacion)
    {
        db.UsuariosComercios.Add(new UsuarioComercio
        {
            UsuarioId = usuario.Id,
            ComercioId = comercioId,
            Rol = "ADMINISTRADOR"
        });

        await db.SaveChangesAsync();
    }
}
}