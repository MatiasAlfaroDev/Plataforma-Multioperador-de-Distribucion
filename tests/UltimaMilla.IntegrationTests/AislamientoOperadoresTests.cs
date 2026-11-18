using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Seed;

namespace UltimaMilla.IntegrationTests;

/// <summary>
/// Un operador no puede leer los datos de otro: se comprueba contra PostgreSQL real, con los
/// filtros globales de consulta que usa la aplicación.
/// </summary>
public class AislamientoOperadoresTests
{
    private static DbContextOptions<AppDbContext> ObtenerOpciones()
    {
        var conexion = Environment.GetEnvironmentVariable(
            "ULTIMAMILLA_INTEGRATION_CONNECTION");

        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException(
                "Falta ULTIMAMILLA_INTEGRATION_CONNECTION.");

        // Protección: impedir ejecutar esta prueba sobre otra base.
        if (new NpgsqlConnectionStringBuilder(conexion).Database != "ultimamilla_integracion")
            throw new InvalidOperationException(
                "Esta prueba solo puede usar ultimamilla_integracion.");

        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .Options;
    }

    private sealed class TenantDePrueba(Guid? operadorId) : ITenantContext
    {
        public Guid? UsuarioId => operadorId is null ? null : Guid.NewGuid();
        public Guid? OperadorId => operadorId;
        public Guid? ComercioId => null;
        public string? Perfil => null;
        public bool EstaAutenticado => operadorId is not null;
    }

    private static async Task<Guid> CrearEnvioDelOperadorAsync(
        DbContextOptions<AppDbContext> opciones,
        Guid operadorId,
        CancellationToken ct)
    {
        await using var db = new AppDbContext(opciones);
        db.HabilitarAccesoInicializacion();

        var cuenta = await db.CuentasComerciales
            .AsNoTracking()
            .FirstAsync(c => c.OperadorId == operadorId && c.Activa, ct);

        var reglas = await db.VersionesReglasOperativas
            .AsNoTracking()
            .Where(r => r.OperadorId == operadorId)
            .OrderByDescending(r => r.VigenciaDesde)
            .FirstAsync(ct);

        var envio = Envio.Crear(
            cuenta,
            $"AISLAMIENTO-{Guid.NewGuid():N}",
            Modalidad.Estandar,
            new Destinatario("Destinatario Test", "099123456"),
            new Direccion("Calle Test", "123", "San José", "San José"),
            [new NuevoBulto(1m, 20, 20, 20)],
            DateTimeOffset.UtcNow);

        envio.AsignarReglasOperativas(reglas.Id);

        db.Envios.Add(envio);
        await db.SaveChangesAsync(ct);

        return envio.Id;
    }

    [Fact]
    public async Task UnOperador_NoLeeLosEnviosNiLasCuentasDeOtro()
    {
        var opciones = ObtenerOpciones();
        var ct = CancellationToken.None;

        await using (var db = new AppDbContext(opciones))
        {
            db.HabilitarAccesoInicializacion();
            await DbSeeder.SembrarAsync(db, ct);
        }

        var envioRapi = await CrearEnvioDelOperadorAsync(
            opciones, DbSeeder.RapiEnviosId, ct);

        var envioSur = await CrearEnvioDelOperadorAsync(
            opciones, DbSeeder.LogisticaSurId, ct);

        // Operador A (RapiEnvíos): ve el suyo y no el del otro.
        await using (var db = new AppDbContext(
            opciones, new TenantDePrueba(DbSeeder.RapiEnviosId)))
        {
            var ids = await db.Envios.Select(e => e.Id).ToListAsync(ct);

            Assert.Contains(envioRapi, ids);
            Assert.DoesNotContain(envioSur, ids);

            Assert.Empty(await db.Envios
                .Where(e => e.Id == envioSur)
                .ToListAsync(ct));

            var cuentas = await db.CuentasComerciales.ToListAsync(ct);

            Assert.NotEmpty(cuentas);
            Assert.All(cuentas, c =>
                Assert.Equal(DbSeeder.RapiEnviosId, c.OperadorId));
        }

        // Operador B (Logística Sur): lo mismo, a la inversa.
        await using (var db = new AppDbContext(
            opciones, new TenantDePrueba(DbSeeder.LogisticaSurId)))
        {
            var ids = await db.Envios.Select(e => e.Id).ToListAsync(ct);

            Assert.Contains(envioSur, ids);
            Assert.DoesNotContain(envioRapi, ids);

            var cuentas = await db.CuentasComerciales.ToListAsync(ct);

            Assert.NotEmpty(cuentas);
            Assert.All(cuentas, c =>
                Assert.Equal(DbSeeder.LogisticaSurId, c.OperadorId));
        }
    }

    [Fact]
    public async Task SinOperadorAutenticado_NoSeVeNingunEnvio()
    {
        var opciones = ObtenerOpciones();

        await using var db = new AppDbContext(opciones, new TenantDePrueba(null));

        Assert.Empty(await db.Envios.ToListAsync());
        Assert.Empty(await db.CuentasComerciales.ToListAsync());
    }
}
