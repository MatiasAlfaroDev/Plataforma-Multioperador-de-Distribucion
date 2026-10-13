
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Auth.Commands.Register;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Identity;

public sealed class RegistrationService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db) : IRegistrationService
{
    public async Task<RegisterResult> RegisterAsync(
        RegisterCommand command,
        CancellationToken ct)
    {
        var email = command.Email.Trim();
        var rut = command.Rut.Trim();

        // Comprobaciones previas para mostrar errores claros.
        if (await userManager.FindByEmailAsync(email) is not null)
            return new RegisterResult(RegisterStatus.EmailYaRegistrado);

        if (await db.Comercios.AnyAsync(c => c.Rut == rut, ct))
            return new RegisterResult(RegisterStatus.RutYaRegistrado);

        await using var transaccion =
            await db.Database.BeginTransactionAsync(ct);

        try
        {
            var comercio = Comercio.Crear(
                Guid.NewGuid(),
                rut,
                command.NombreComercio);

            db.Comercios.Add(comercio);

            var usuario = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                NombreCompleto = command.NombreCompleto.Trim(),
                Activo = true
            };

            var resultadoUsuario = await userManager.CreateAsync(
                usuario,
                command.Password);

            if (!resultadoUsuario.Succeeded)
            {
                await transaccion.RollbackAsync(ct);

                return new RegisterResult(
                    RegisterStatus.DatosInvalidos,
                    Errores: resultadoUsuario.Errors
                        .Select(e => e.Description)
                        .ToArray());
            }

            db.UsuariosComercios.Add(new UsuarioComercio
            {
                UsuarioId = usuario.Id,
                ComercioId = comercio.Id,
                Rol = "ADMINISTRADOR"
            });

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            return new RegisterResult(
                RegisterStatus.Exitoso,
                new RegisterResponse(
                    usuario.Id,
                    comercio.Id,
                    comercio.Nombre,
                    email));
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            await transaccion.RollbackAsync(ct);

            if (pg.ConstraintName?.Contains(
                    "Rut", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new RegisterResult(
                    RegisterStatus.RutYaRegistrado);
            }

            if (pg.ConstraintName?.Contains(
                    "NormalizedEmail", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new RegisterResult(
                    RegisterStatus.EmailYaRegistrado);
            }

            throw;
        }
    }
}
