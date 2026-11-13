using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Rutas;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Seed;

namespace UltimaMilla.IntegrationTests;

/// <summary>
/// Concurrencia optimista sobre la hoja de ruta (informe 6.4): dos despachadores que editan la
/// misma ruta a la vez no pueden pisarse, así no se pasan de la capacidad del vehículo.
/// </summary>
public class HojaDeRutaConcurrenciaTests
{
    [Fact]
    public async Task DosDespachadoresAgregandoEnviosALaMismaRuta_UnoRecibeConflicto()
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
        Guid hojaId;
        Guid envioAId;
        Guid envioBId;

        // Preparar una ruta vacía y dos envíos en depósito, exclusivos de esta prueba.
        await using (var db = new AppDbContext(opciones))
        {
            db.HabilitarAccesoInicializacion();
            await DbSeeder.SembrarAsync(db, CancellationToken.None);

            var cuenta = await db.CuentasComerciales
                .AsNoTracking()
                .FirstAsync(c => c.Activa);

            var reglas = await db.VersionesReglasOperativas
                .AsNoTracking()
                .Where(r => r.OperadorId == cuenta.OperadorId)
                .OrderByDescending(r => r.VigenciaDesde)
                .FirstAsync();

            Envio NuevoEnDeposito(string prefijo)
            {
                var envio = Envio.Crear(
                    cuenta,
                    $"{prefijo}-{Guid.NewGuid():N}",
                    Modalidad.Estandar,
                    new Destinatario("Destinatario Test", "099123456"),
                    new Direccion("Calle Test", "123", "San José", "San José"),
                    [new NuevoBulto(2m, 20, 20, 20)],
                    ahora);

                envio.AsignarReglasOperativas(reglas.Id);
                envio.Aplicar(
                    DisparadorEnvio.RecibirEnDeposito,
                    reglas,
                    ahora,
                    "PruebaIntegracion",
                    "Operario");

                return envio;
            }

            var envioA = NuevoEnDeposito("RUTA-A");
            var envioB = NuevoEnDeposito("RUTA-B");

            var repartidor = Repartidor.Crear(
                cuenta.OperadorId, "Repartidor Test", "099123456");

            var vehiculo = Vehiculo.Crear(
                cuenta.OperadorId,
                $"T{Guid.NewGuid():N}"[..10],
                100m,
                10m);

            var hoja = HojaDeRuta.Crear(
                cuenta.OperadorId,
                repartidor,
                vehiculo,
                DateOnly.FromDateTime(ahora.UtcDateTime),
                10);

            db.Envios.AddRange(envioA, envioB);
            db.Repartidores.Add(repartidor);
            db.Vehiculos.Add(vehiculo);
            db.HojasDeRuta.Add(hoja);
            await db.SaveChangesAsync();

            hojaId = hoja.Id;
            envioAId = envioA.Id;
            envioBId = envioB.Id;
        }

        await using var contexto1 = new AppDbContext(opciones);
        await using var contexto2 = new AppDbContext(opciones);

        contexto1.HabilitarAccesoInicializacion();
        contexto2.HabilitarAccesoInicializacion();

        // Los dos despachadores abren la misma ruta, con el mismo estado inicial.
        var hoja1 = await contexto1.HojasDeRuta
            .Include(h => h.Paradas)
            .SingleAsync(h => h.Id == hojaId);

        var hoja2 = await contexto2.HojasDeRuta
            .Include(h => h.Paradas)
            .SingleAsync(h => h.Id == hojaId);

        var envio1 = await contexto1.Envios
            .Include(e => e.Bultos)
            .SingleAsync(e => e.Id == envioAId);

        var envio2 = await contexto2.Envios
            .Include(e => e.Bultos)
            .SingleAsync(e => e.Id == envioBId);

        // El primero agrega su envío y guarda.
        hoja1.AgregarEnvio(envio1);
        await contexto1.SaveChangesAsync();

        // El segundo decidió sobre una ruta que ya cambió: su guardado tiene que fallar.
        hoja2.AgregarEnvio(envio2);

        // Ambos agregaron la parada en la posición 1: el índice único (HojaDeRutaId, Orden)
        // frena al segundo antes de que EF llegue a comparar el token. Lo importante es que
        // falla y no se pisa nada (la API lo traduce a 409).
        await Assert.ThrowsAnyAsync<DbUpdateException>(
            () => contexto2.SaveChangesAsync());

        // La ruta quedó solo con el envío del primero: no hubo pisadas.
        await using var verificacion = new AppDbContext(opciones);
        verificacion.HabilitarAccesoInicializacion();

        var resultado = await verificacion.HojasDeRuta
            .AsNoTracking()
            .Include(h => h.Paradas)
            .SingleAsync(h => h.Id == hojaId);

        var parada = Assert.Single(resultado.Paradas);
        Assert.Equal(envioAId, parada.EnvioId);
        Assert.Equal(2m, resultado.PesoAsignadoKg);

        // Segundo escenario: un cambio que NO choca con ningún índice único (la hora de salida).
        // Acá la única defensa es el token xmin de la hoja, así que se prueba el token en sí.
        await using var contexto3 = new AppDbContext(opciones);
        await using var contexto4 = new AppDbContext(opciones);

        contexto3.HabilitarAccesoInicializacion();
        contexto4.HabilitarAccesoInicializacion();

        var hoja3 = await contexto3.HojasDeRuta.SingleAsync(h => h.Id == hojaId);
        var hoja4 = await contexto4.HojasDeRuta.SingleAsync(h => h.Id == hojaId);

        hoja3.ConfigurarHoraSalida(new TimeOnly(8, 0));
        await contexto3.SaveChangesAsync();

        hoja4.ConfigurarHoraSalida(new TimeOnly(9, 0));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => contexto4.SaveChangesAsync());

        await using var verificacion2 = new AppDbContext(opciones);
        verificacion2.HabilitarAccesoInicializacion();

        var final = await verificacion2.HojasDeRuta
            .AsNoTracking()
            .SingleAsync(h => h.Id == hojaId);

        Assert.Equal(new TimeOnly(8, 0), final.HoraSalida);
    }
}