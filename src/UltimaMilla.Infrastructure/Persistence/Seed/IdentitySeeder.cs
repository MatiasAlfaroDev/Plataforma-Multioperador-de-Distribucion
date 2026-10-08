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
}