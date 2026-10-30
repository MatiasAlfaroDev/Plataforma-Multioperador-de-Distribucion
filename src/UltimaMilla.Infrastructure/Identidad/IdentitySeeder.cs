using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Seed;

namespace UltimaMilla.Infrastructure.Identidad;

public static class IdentitySeeder
{
    public static async Task SembrarAsync(
        UserManager<Usuario> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext db,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        // Los usuarios demo solo se crean si está habilitado
        // explícitamente en la configuración.
        if (!bool.TryParse(
        configuration["Inicializacion:UsuariosDemo"],
        out var usuariosDemoHabilitados)
    || !usuariosDemoHabilitados)
        {
            return;
        }

        var password = configuration["Inicializacion:PasswordDemo"];

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Falta Inicializacion:PasswordDemo.");

        foreach (var perfil in new[]
        {
            Perfiles.AdministradorPlataforma,
            Perfiles.AdministradorOperador,
            Perfiles.Operador,
            Perfiles.Comercio,
            Perfiles.Repartidor
        })
        {
            if (!await roleManager.RoleExistsAsync(perfil))
            {
                var resultado = await roleManager.CreateAsync(
                    new IdentityRole<Guid>(perfil));

                Verificar(resultado);
            }
        }

        await CrearUsuarioAsync(
            "admin.rapi@demo.local",
            "Administrador RapiEnvíos",
            Perfiles.AdministradorOperador,
            DbSeeder.RapiEnviosId,
            null);

        await CrearUsuarioAsync(
            "admin.sur@demo.local",
            "Administrador Logística Sur",
            Perfiles.AdministradorOperador,
            DbSeeder.LogisticaSurId,
            null);

        await CrearUsuarioAsync(
            "tienda.norte@demo.local",
            "Tienda Norte",
            Perfiles.Comercio,
            DbSeeder.RapiEnviosId,
            Guid.Parse("33333333-0000-0000-0000-000000000001"));

        await CrearUsuarioAsync(
            "tienda.norte@demo.local",
            "Tienda Norte",
            Perfiles.Comercio,
            DbSeeder.LogisticaSurId,
            Guid.Parse("33333333-0000-0000-0000-000000000002"));

        // Repartidores de demo: inician sesión en la app y quedan vinculados al
        // "Repartidor de prueba" de su operador (el que sembró DbSeeder).
        await VincularRepartidorAsync(
            await CrearUsuarioAsync(
                "repartidor.rapi@demo.local",
                "Repartidor RapiEnvíos",
                Perfiles.Repartidor,
                DbSeeder.RapiEnviosId,
                null),
            DbSeeder.RapiEnviosId);

        await VincularRepartidorAsync(
            await CrearUsuarioAsync(
                "repartidor.sur@demo.local",
                "Repartidor Logística Sur",
                Perfiles.Repartidor,
                DbSeeder.LogisticaSurId,
                null),
            DbSeeder.LogisticaSurId);

        async Task VincularRepartidorAsync(Usuario usuario, Guid operadorId)
        {
            var yaVinculado = await db.Repartidores
                .AnyAsync(r => r.OperadorId == operadorId && r.UsuarioId == usuario.Id, ct);

            if (yaVinculado)
                return;

            var libre = await db.Repartidores
                .Where(r => r.OperadorId == operadorId && r.UsuarioId == null && r.Activo)
                .OrderBy(r => r.Nombre)
                .FirstOrDefaultAsync(ct);

            libre?.VincularUsuario(usuario.Id);
        }

        async Task<Usuario> CrearUsuarioAsync(
            string email,
            string nombre,
            string perfil,
            Guid operadorId,
            Guid? cuentaComercialId)
        {
            var usuario = await userManager.FindByEmailAsync(email);

            if (usuario is null)
            {
                usuario = new Usuario
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    NombreCompleto = nombre,
                    Activo = true,
                    EmailConfirmed = true
                };

                Verificar(await userManager.CreateAsync(usuario, password));
            }else if (
                bool.TryParse(
                    configuration["Inicializacion:RestablecerPasswordDemo"],
                    out var restablecerPassword)
                && restablecerPassword)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(usuario);

                Verificar(await userManager.ResetPasswordAsync(
                    usuario,
                    token,
                    password));
            }

            if (!await userManager.IsInRoleAsync(usuario, perfil))
            {
                Verificar(await userManager.AddToRoleAsync(usuario, perfil));
            }

            var existeMembresia = await db.MembresiasUsuarios
                .AnyAsync(m =>
                    m.UsuarioId == usuario.Id &&
                    m.OperadorId == operadorId &&
                    m.CuentaComercialId == cuentaComercialId &&
                    m.Perfil == perfil, ct);

            if (!existeMembresia)
            {
                db.MembresiasUsuarios.Add(new MembresiaUsuario
                {
                    UsuarioId = usuario.Id,
                    OperadorId = operadorId,
                    CuentaComercialId = cuentaComercialId,
                    Perfil = perfil,
                    Activa = true
                });
            }

            return usuario;
        }

        await db.SaveChangesAsync(ct);
    }

    private static void Verificar(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ",
                    resultado.Errors.Select(e => e.Description)));
        }
    }
}