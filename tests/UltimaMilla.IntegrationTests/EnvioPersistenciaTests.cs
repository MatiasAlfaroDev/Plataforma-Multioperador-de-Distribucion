
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Seed;

namespace UltimaMilla.IntegrationTests;

public class EnvioPersistenciaTests
{
    [Fact]
    public async Task RecibirEnDeposito_GuardaEstadoEHistorialEnPostgreSql()
    {
        var conexion = Environment.GetEnvironmentVariable(
            "ULTIMAMILLA_INTEGRATION_CONNECTION");

        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException(
                "Falta ULTIMAMILLA_INTEGRATION_CONNECTION.");

        // Protección: impedir ejecutar esta prueba sobre otra base.
        var datosConexion = new NpgsqlConnectionStringBuilder(conexion);

        if (datosConexion.Database != "ultimamilla_integracion")
            throw new InvalidOperationException(
                "Esta prueba solo puede usar ultimamilla_integracion.");

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .Options;

        var ct = CancellationToken.None;
        var ahora = DateTimeOffset.UtcNow;
        Guid envioId;

        // Preparación de los datos de referencia.
        await using (var db = new AppDbContext(opciones))
        {
            await DbSeeder.SembrarAsync(db, ct);

            var cuenta = await db.CuentasComerciales
                .AsNoTracking()
                .FirstAsync(c => c.Activa, ct);

            var reglas = await db.VersionesReglasOperativas
                .AsNoTracking()
                .Where(r => r.OperadorId == cuenta.OperadorId)
                .OrderByDescending(r => r.VigenciaDesde)
                .FirstAsync(ct);

            var envio = Envio.Crear(
                cuenta,
                $"INTEGRACION-{Guid.NewGuid():N}",
                Modalidad.Estandar,
                new Destinatario("Destinatario Test", "099123456"),
                new Direccion("Calle Test", "123", "San José", "San José"),
                [new NuevoBulto(2m, 20, 20, 20)],
                ahora);

            envio.AsignarReglasOperativas(reglas.Id);

            db.Envios.Add(envio);
            await db.SaveChangesAsync(ct);

            envioId = envio.Id;
        }

        // Otro contexto recupera el envío y ejecuta la transición.
        await using (var db = new AppDbContext(opciones))
        {
            var envio = await db.Envios
                .Include(e => e.Eventos)
                .Include(e => e.Intentos)
                .AsSplitQuery()
                .SingleAsync(e => e.Id == envioId, ct);

            Assert.Equal(EstadoEnvio.Admitido, envio.Estado);

            var reglas = await db.VersionesReglasOperativas
                .SingleAsync(
                    r => r.Id == envio.VersionReglasOperativasId,
                    ct);

            envio.Aplicar(
                DisparadorEnvio.RecibirEnDeposito,
                reglas,
                ahora,
                "PruebaIntegracion",
                "OperarioTest");

            await db.SaveChangesAsync(ct);
        }

        // Un tercer contexto comprueba lo realmente persistido.
        await using (var db = new AppDbContext(opciones))
        {
            var envio = await db.Envios
                .AsNoTracking()
                .Include(e => e.Eventos)
                .AsSplitQuery()
                .SingleAsync(e => e.Id == envioId, ct);

            Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);

            var evento = Assert.Single(envio.Eventos);

            Assert.Equal(EstadoEnvio.Admitido, evento.EstadoAnterior);
            Assert.Equal(EstadoEnvio.EnDeposito, evento.EstadoNuevo);
            Assert.Equal(
                DisparadorEnvio.RecibirEnDeposito,
                evento.Disparador);
            Assert.Equal("PruebaIntegracion", evento.Origen);
            Assert.Equal("OperarioTest", evento.Responsable);
        }
    }
    
    [Fact]
    public async Task DosOperacionesSimultaneas_NoSobrescribenElEstado()
    {
        var conexion = Environment.GetEnvironmentVariable(
            "ULTIMAMILLA_INTEGRATION_CONNECTION");

        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException(
                "Falta ULTIMAMILLA_INTEGRATION_CONNECTION.");

        var datosConexion = new NpgsqlConnectionStringBuilder(conexion);

        if (datosConexion.Database != "ultimamilla_integracion")
            throw new InvalidOperationException(
                "Solo se permite utilizar ultimamilla_integracion.");

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .Options;

        var ahora = DateTimeOffset.UtcNow;
        Guid envioId;

        // Crear un envío exclusivo para esta prueba.
        await using (var db = new AppDbContext(opciones))
        {
            await DbSeeder.SembrarAsync(db, CancellationToken.None);

            var cuenta = await db.CuentasComerciales
                .AsNoTracking()
                .FirstAsync(c => c.Activa);

            var reglas = await db.VersionesReglasOperativas
                .AsNoTracking()
                .Where(r => r.OperadorId == cuenta.OperadorId)
                .OrderByDescending(r => r.VigenciaDesde)
                .FirstAsync();

            var envio = Envio.Crear(
                cuenta,
                $"CONCURRENCIA-{Guid.NewGuid():N}",
                Modalidad.Estandar,
                new Destinatario("Destinatario Test", "099123456"),
                new Direccion("Calle Test", "123", "San José", "San José"),
                [new NuevoBulto(2m, 20, 20, 20)],
                ahora);

            envio.AsignarReglasOperativas(reglas.Id);

            db.Envios.Add(envio);
            await db.SaveChangesAsync();

            envioId = envio.Id;
        }

        await using var contexto1 = new AppDbContext(opciones);
        await using var contexto2 = new AppDbContext(opciones);

        // Los dos contextos leen el mismo estado inicial.
        var envio1 = await contexto1.Envios
            .Include(e => e.Eventos)
            .SingleAsync(e => e.Id == envioId);

        var envio2 = await contexto2.Envios
            .Include(e => e.Eventos)
            .SingleAsync(e => e.Id == envioId);

        var reglas1 = await contexto1.VersionesReglasOperativas
            .SingleAsync(r => r.Id == envio1.VersionReglasOperativasId);

        var reglas2 = await contexto2.VersionesReglasOperativas
            .SingleAsync(r => r.Id == envio2.VersionReglasOperativasId);

        // Primera operación: recibe el envío.
        envio1.Aplicar(
            DisparadorEnvio.RecibirEnDeposito,
            reglas1,
            ahora,
            "PruebaIntegracion",
            "Operario1");

        await contexto1.SaveChangesAsync();

        // Segunda operación: intenta guardar una transición
        // basada en el estado anterior del mismo envío.
        envio2.Aplicar(
            DisparadorEnvio.RecibirEnDeposito,
            reglas2,
            ahora.AddSeconds(1),
            "PruebaIntegracion",
            "Operario2");

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => contexto2.SaveChangesAsync());

        // Verificar que el segundo intento no sobrescribió el primero.
        await using var verificacion = new AppDbContext(opciones);

        var resultado = await verificacion.Envios
            .AsNoTracking()
            .Include(e => e.Eventos)
            .SingleAsync(e => e.Id == envioId);

        Assert.Equal(EstadoEnvio.EnDeposito, resultado.Estado);

        var evento = Assert.Single(resultado.Eventos);
        Assert.Equal("Operario1", evento.Responsable);
    }

}
