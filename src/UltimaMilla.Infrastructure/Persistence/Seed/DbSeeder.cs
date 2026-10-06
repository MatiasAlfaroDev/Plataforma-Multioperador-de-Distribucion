using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos iniciales idempotentes: si ya existen, no hace nada (como la demo Support).
/// Dos operadores con configuraciones distintas, y un comercio que trabaja con los dos
/// para mostrar el rol de CuentaComercial.
/// </summary>
public static class DbSeeder
{
    public static readonly Guid RapiEnviosId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid LogisticaSurId = Guid.Parse("11111111-0000-0000-0000-000000000002");

    public static readonly Guid TiendaNorteId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    public static readonly Guid ModaExpressId = Guid.Parse("22222222-0000-0000-0000-000000000002");

    public static async Task SembrarAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Operadores.AnyAsync(ct))
            return;

        db.Operadores.AddRange(
            Operador.Crear(RapiEnviosId, "RapiEnvíos", "#1E5AA8", "contacto@rapienvios.uy"),
            Operador.Crear(LogisticaSurId, "Logística Sur", "#2E7D32", "hola@logisticasur.uy"));

        db.Comercios.AddRange(
            Comercio.Crear(TiendaNorteId, "210000000011", "Tienda Norte"),
            Comercio.Crear(ModaExpressId, "210000000022", "Moda Express"));

        db.CuentasComerciales.AddRange(
            CuentaComercial.Crear(Guid.Parse("33333333-0000-0000-0000-000000000001"), RapiEnviosId, TiendaNorteId),
            CuentaComercial.Crear(Guid.Parse("33333333-0000-0000-0000-000000000002"), LogisticaSurId, TiendaNorteId),
            CuentaComercial.Crear(Guid.Parse("33333333-0000-0000-0000-000000000003"), RapiEnviosId, ModaExpressId));

        await db.SaveChangesAsync(ct);
    }
}
