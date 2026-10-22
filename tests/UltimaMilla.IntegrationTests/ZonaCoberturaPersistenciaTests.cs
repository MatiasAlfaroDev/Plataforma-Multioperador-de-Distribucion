
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Seed;
using UltimaMilla.Infrastructure.Repositories;

namespace UltimaMilla.IntegrationTests;

public class ZonaCoberturaPersistenciaTests
{
    [Fact]
    public async Task CrearZona_GuardaYRecuperaCoberturasYFranjas()
    {
        var conexion = Environment.GetEnvironmentVariable(
            "ULTIMAMILLA_INTEGRATION_CONNECTION");

        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException(
                "Falta ULTIMAMILLA_INTEGRATION_CONNECTION.");

        var datosConexion = new NpgsqlConnectionStringBuilder(conexion);

        if (!string.Equals(
            datosConexion.Database,
            "ultimamilla_integracion",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Esta prueba solo puede utilizar ultimamilla_integracion.");
        }

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .Options;

        var ct = CancellationToken.None;
        Guid zonaId;
        Guid operadorId;

        // Crear una zona completa y persistirla.
        await using (var db = new AppDbContext(opciones))
        {
            await DbSeeder.SembrarAsync(db, ct);

            operadorId = await db.CuentasComerciales
                .AsNoTracking()
                .Where(c => c.Activa)
                .Select(c => c.OperadorId)
                .FirstAsync(ct);

            var zona = ZonaCobertura.Crear(
                operadorId,
                $"Zona integración {Guid.NewGuid():N}",
                "San José");

            zona.AgregarLocalidad("San José de Mayo");
            zona.AgregarSector("Ciudad del Plata", "Sector Norte");

            zona.AgregarFranja(
                new TimeOnly(9, 0),
                new TimeOnly(12, 0));

            zona.AgregarFranja(
                new TimeOnly(14, 0),
                new TimeOnly(18, 0));

            zona.ConfigurarTiempoEstimado(15);

            db.ZonasCobertura.Add(zona);
            await db.SaveChangesAsync(ct);

            zonaId = zona.Id;
        }

        // Otro contexto recupera los datos desde PostgreSQL,
        // utilizando el repositorio real.
        await using (var db = new AppDbContext(opciones))
        {
            var repositorio = new ZonaCoberturaRepository(db);

            var zonas = await repositorio.ListarPorOperadorAsync(
                operadorId,
                ct);

            var recuperada = Assert.Single(
                zonas, z => z.Id == zonaId);

            Assert.Equal("San José", recuperada.Departamento);
            Assert.True(recuperada.Activa);

            Assert.Equal(15, recuperada.MinutosTrasladoEstimado);

            Assert.Equal(2, recuperada.Localidades.Count);
            Assert.Equal(2, recuperada.Franjas.Count);

            Assert.True(recuperada.CubreDireccion(
                "San José de Mayo",
                null));

            Assert.True(recuperada.CubreDireccion(
                "Ciudad del Plata",
                "Sector Norte"));

            Assert.False(recuperada.CubreDireccion(
                "Ciudad del Plata",
                "Sector Sur"));

            Assert.True(recuperada.OfreceFranja(
                new TimeOnly(9, 0),
                new TimeOnly(12, 0)));

            Assert.True(recuperada.OfreceFranja(
                new TimeOnly(14, 0),
                new TimeOnly(18, 0)));
        }
    }
}
