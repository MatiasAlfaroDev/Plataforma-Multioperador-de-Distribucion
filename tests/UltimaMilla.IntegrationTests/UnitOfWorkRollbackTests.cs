
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Seed;
using UltimaMilla.Infrastructure.Repositories;

namespace UltimaMilla.IntegrationTests;

public class UnitOfWorkRollbackTests
{
    [Fact]
    public async Task ErrorDespuesDeDosGuardados_RevierteTodaLaTransaccion()
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
                "La prueba solo puede usar ultimamilla_integracion.");
        }

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .Options;

        var ct = CancellationToken.None;
        var ahora = DateTimeOffset.UtcNow;
        Guid envioId;

        // Preparar un envío exclusivo, inicialmente Admitido.
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
                $"ROLLBACK-{Guid.NewGuid():N}",
                Modalidad.Estandar,
                new Destinatario("Destinatario Test", "099123456"),
                new Direccion(
                    "Calle Test", "123", "San José", "San José"),
                [new NuevoBulto(1m, 20, 20, 20)],
                ahora);

            envio.AsignarReglasOperativas(reglas.Id);

            db.Envios.Add(envio);
            await db.SaveChangesAsync(ct);

            envioId = envio.Id;
        }

        // Ejecutar dos cambios y guardarlos dentro de una transacción.
        await using (var db = new AppDbContext(opciones))
        {
            var unidad = new UnitOfWork(db);

            var envio = await db.Envios
                .Include(e => e.Eventos)
                .AsSplitQuery()
                .SingleAsync(e => e.Id == envioId, ct);

            var reglas = await db.VersionesReglasOperativas
                .SingleAsync(
                    r => r.Id == envio.VersionReglasOperativasId,
                    ct);

            var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
                () => unidad.EjecutarEnTransaccionAsync(
                    async transaccionCt =>
                    {
                        envio.Aplicar(
                            DisparadorEnvio.RecibirEnDeposito,
                            reglas,
                            ahora,
                            "PruebaIntegracion",
                            "OperarioTest");

                        await unidad.GuardarCambiosAsync(transaccionCt);

                        envio.Aplicar(
                            DisparadorEnvio.DeclararExtraviado,
                            reglas,
                            ahora.AddMinutes(1),
                            "PruebaIntegracion",
                            "OperarioTest",
                            justificacion: "Extravío de prueba");

                        await unidad.GuardarCambiosAsync(transaccionCt);

                        // Simular un fallo posterior al segundo guardado.
                        throw new InvalidOperationException(
                            "Fallo simulado antes del commit.");
                    },
                    ct));

            Assert.Equal(
                "Fallo simulado antes del commit.",
                excepcion.Message);
        }

        // Otro contexto verifica lo que realmente quedó en PostgreSQL.
        await using (var db = new AppDbContext(opciones))
        {
            var envio = await db.Envios
                .AsNoTracking()
                .Include(e => e.Eventos)
                .AsSplitQuery()
                .SingleAsync(e => e.Id == envioId, ct);

            Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
            Assert.Empty(envio.Eventos);
        }
    }
}
