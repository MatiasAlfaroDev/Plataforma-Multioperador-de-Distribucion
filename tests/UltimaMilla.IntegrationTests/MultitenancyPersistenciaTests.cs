
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Interceptors;
using UltimaMilla.Infrastructure.Persistence.Seed;
using UltimaMilla.IntegrationTests.Seguridad;

namespace UltimaMilla.IntegrationTests;

public class MultitenancyPersistenciaTests
{
    private static string ObtenerConexion()
    {
        var conexion = Environment.GetEnvironmentVariable(
            "ULTIMAMILLA_INTEGRATION_CONNECTION");

        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException(
                "Falta ULTIMAMILLA_INTEGRATION_CONNECTION.");

        var datos = new NpgsqlConnectionStringBuilder(conexion);

        if (!string.Equals(
                datos.Database,
                "ultimamilla_integracion",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Las pruebas solo pueden usar ultimamilla_integracion.");

        return conexion;
    }

    private static AppDbContext CrearContexto(
        string conexion,
        TestTenantContext tenant)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .AddInterceptors(new TenantSaveChangesInterceptor(tenant))
            .Options;

        return new AppDbContext(opciones, tenant);
    }

    private static async Task PrepararDatosAsync(string conexion)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .Options;

        await using var db = new AppDbContext(opciones);

        db.HabilitarAccesoInicializacion();

        await DbSeeder.SembrarAsync(db);
    }

    [Fact]
    public async Task OperadorA_SoloPuedeConsultarSusZonas()
    {
        var conexion = ObtenerConexion();

        await PrepararDatosAsync(conexion);

        var tenantA = new TestTenantContext
        {
            OperadorId = DbSeeder.RapiEnviosId
        };

        var tenantB = new TestTenantContext
        {
            OperadorId = DbSeeder.LogisticaSurId
        };

        await using var dbA = CrearContexto(conexion, tenantA);
        await using var dbB = CrearContexto(conexion, tenantB);

        var zonasA = await dbA.ZonasCobertura
            .AsNoTracking()
            .ToListAsync();

        var zonasB = await dbB.ZonasCobertura
            .AsNoTracking()
            .ToListAsync();

        Assert.NotEmpty(zonasA);
        Assert.NotEmpty(zonasB);

        Assert.All(zonasA, zona =>
            Assert.Equal(DbSeeder.RapiEnviosId, zona.OperadorId));

        Assert.All(zonasB, zona =>
            Assert.Equal(DbSeeder.LogisticaSurId, zona.OperadorId));

        Assert.DoesNotContain(zonasA, zona =>
            zona.OperadorId == DbSeeder.LogisticaSurId);

        Assert.DoesNotContain(zonasB, zona =>
            zona.OperadorId == DbSeeder.RapiEnviosId);
    }

    [Fact]
    public async Task OperadorA_PuedeGuardarSuPropiaZona()
    {
        var conexion = ObtenerConexion();

        await PrepararDatosAsync(conexion);

        var tenant = new TestTenantContext
        {
            OperadorId = DbSeeder.RapiEnviosId
        };

        await using var db = CrearContexto(conexion, tenant);
        await using var transaccion =
            await db.Database.BeginTransactionAsync();

        var zona = ZonaCobertura.Crear(
            DbSeeder.RapiEnviosId,
            $"Zona test {Guid.NewGuid():N}",
            "San José");

        db.ZonasCobertura.Add(zona);

        await db.SaveChangesAsync();

        Assert.True(await db.ZonasCobertura
            .AnyAsync(z => z.Id == zona.Id));

        // Al no confirmar la transacción, se revierten
        // los datos al finalizar la prueba.
    }

    [Fact]
    public async Task OperadorA_NoPuedeCrearZonaDeOtroOperador()
    {
        var conexion = ObtenerConexion();

        await PrepararDatosAsync(conexion);

        var tenant = new TestTenantContext
        {
            OperadorId = DbSeeder.RapiEnviosId
        };

        await using var db = CrearContexto(conexion, tenant);

        var zonaAjena = ZonaCobertura.Crear(
            DbSeeder.LogisticaSurId,
            $"Zona ajena {Guid.NewGuid():N}",
            "San José");

        db.ZonasCobertura.Add(zonaAjena);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task UsuarioSinOperador_NoPuedeGuardarZona()
    {
        var conexion = ObtenerConexion();

        await PrepararDatosAsync(conexion);

        var tenant = new TestTenantContext
        {
            OperadorId = null
        };

        await using var db = CrearContexto(conexion, tenant);

        var zona = ZonaCobertura.Crear(
            DbSeeder.RapiEnviosId,
            $"Zona sin operador {Guid.NewGuid():N}",
            "San José");

        db.ZonasCobertura.Add(zona);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => db.SaveChangesAsync());
    }


[Fact]
public async Task OperadorA_NoPuedeModificarZonaDeOtroOperador()
{
    var conexion = ObtenerConexion();

    await PrepararDatosAsync(conexion);

    var tenant = new TestTenantContext
    {
        OperadorId = DbSeeder.RapiEnviosId
    };

    await using var db = CrearContexto(conexion, tenant);

    // Se consulta deliberadamente sin filtros para
    // simular que se obtuvo una entidad ajena.
    var zonaAjena = await db.ZonasCobertura
        .IgnoreQueryFilters()
        .AsNoTracking()
        .FirstAsync(z =>
            z.OperadorId == DbSeeder.LogisticaSurId);

    // Se adjunta al contexto y se intenta modificar.
    db.ZonasCobertura.Attach(zonaAjena);

    zonaAjena.Desactivar();

    // Marcamos explícitamente la modificación.
    db.Entry(zonaAjena).Property(z => z.Activa)
        .IsModified = true;

    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => db.SaveChangesAsync());
}

[Fact]
public async Task OperadorA_NoPuedeEliminarZonaDeOtroOperador()
{
    var conexion = ObtenerConexion();

    await PrepararDatosAsync(conexion);

    var tenant = new TestTenantContext
    {
        OperadorId = DbSeeder.RapiEnviosId
    };

    await using var db = CrearContexto(conexion, tenant);

    var zonaAjena = await db.ZonasCobertura
        .IgnoreQueryFilters()
        .AsNoTracking()
        .FirstAsync(z =>
            z.OperadorId == DbSeeder.LogisticaSurId);

    db.ZonasCobertura.Remove(zonaAjena);

    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => db.SaveChangesAsync());
}

[Fact]
public async Task OperadorA_NoPuedeCambiarOperadorIdDeUnaZona()
{
    var conexion = ObtenerConexion();

    await PrepararDatosAsync(conexion);

    var tenant = new TestTenantContext
    {
        OperadorId = DbSeeder.RapiEnviosId
    };

    await using var db = CrearContexto(conexion, tenant);

    // Obtenemos una zona que pertenece a RapiEnvíos.
    var zonaPropia = await db.ZonasCobertura
        .FirstAsync(z =>
            z.OperadorId == DbSeeder.RapiEnviosId);

    // Simulamos que alguien intenta transferir la zona
    // al operador Logística Sur.
    var propiedad = db.Entry(zonaPropia)
        .Property(z => z.OperadorId);

    propiedad.CurrentValue = DbSeeder.LogisticaSurId;
    propiedad.IsModified = true;

    // El interceptor debe impedir el cambio.
    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => db.SaveChangesAsync());
}

}
