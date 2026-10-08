using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos iniciales idempotentes para la demo.
/// Los datos globales se crean sin tenant.
/// Las entidades tenant se crean dentro del contexto del operador correspondiente.
/// </summary>
public static class DbSeeder
{
    public static readonly Guid RapiEnviosId =
        Guid.Parse("11111111-0000-0000-0000-000000000001");

    public static readonly Guid LogisticaSurId =
        Guid.Parse("11111111-0000-0000-0000-000000000002");

    public static readonly Guid TiendaNorteId =
        Guid.Parse("22222222-0000-0000-0000-000000000001");

    public static readonly Guid ModaExpressId =
        Guid.Parse("22222222-0000-0000-0000-000000000002");

    public static async Task SembrarAsync(
        AppDbContext db,
        ITenantScope tenantScope,
        CancellationToken ct = default)
    {
        if (await db.Operadores.AnyAsync(ct))
            return;

        // 1. Datos globales.
        // Operador y Comercio no implementan ITenantEntity.
        db.Operadores.AddRange(
            Operador.Crear(
                RapiEnviosId,
                "RapiEnvíos",
                "#1E5AA8",
                "contacto@rapienvios.uy"),

            Operador.Crear(
                LogisticaSurId,
                "Logística Sur",
                "#2E7D32",
                "hola@logisticasur.uy"));

        db.Comercios.AddRange(
            Comercio.Crear(
                TiendaNorteId,
                "210000000011",
                "Tienda Norte"),

            Comercio.Crear(
                ModaExpressId,
                "210000000022",
                "Moda Express"));

        await db.SaveChangesAsync(ct);

        // 2. Cuentas comerciales de RapiEnvíos.
        using (tenantScope.UsarTenant(RapiEnviosId))
        {
            db.CuentasComerciales.AddRange(
                CuentaComercial.Crear(
                    Guid.Parse("33333333-0000-0000-0000-000000000001"),
                    RapiEnviosId,
                    TiendaNorteId),

                CuentaComercial.Crear(
                    Guid.Parse("33333333-0000-0000-0000-000000000003"),
                    RapiEnviosId,
                    ModaExpressId));

            await db.SaveChangesAsync(ct);
        }

        // 3. Cuenta comercial de Logística Sur.
        using (tenantScope.UsarTenant(LogisticaSurId))
        {
            db.CuentasComerciales.Add(
                CuentaComercial.Crear(
                    Guid.Parse("33333333-0000-0000-0000-000000000002"),
                    LogisticaSurId,
                    TiendaNorteId));

            await db.SaveChangesAsync(ct);
        }
    }
}