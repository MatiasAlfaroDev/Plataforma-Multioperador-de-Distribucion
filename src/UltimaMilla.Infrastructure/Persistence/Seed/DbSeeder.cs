using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Configuracion;
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

    public static async Task SembrarAsync(
        AppDbContext db,
        CancellationToken ct = default)
    {
        if (!await db.Operadores.AnyAsync(ct))
        {
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

            db.CuentasComerciales.AddRange(
                CuentaComercial.Crear(
                    Guid.Parse("33333333-0000-0000-0000-000000000001"),
                    RapiEnviosId,
                    TiendaNorteId),

                CuentaComercial.Crear(
                    Guid.Parse("33333333-0000-0000-0000-000000000002"),
                    LogisticaSurId,
                    TiendaNorteId),

                CuentaComercial.Crear(
                    Guid.Parse("33333333-0000-0000-0000-000000000003"),
                    RapiEnviosId,
                    ModaExpressId));

            await db.SaveChangesAsync(ct);
        }

        if (!await db.ZonasCobertura.AnyAsync(ct))
        {
            var zonaRapi = ZonaCobertura.Crear(
                RapiEnviosId,
                "San José",
                "San José");

            var cuadroRapi = CuadroTarifario.Crear(
                RapiEnviosId,
                "Tarifa general");

            var zonaLogistica = ZonaCobertura.Crear(
                LogisticaSurId,
                "San José",
                "San José");

            var cuadroLogistica = CuadroTarifario.Crear(
                LogisticaSurId,
                "Tarifa general");

            db.ZonasCobertura.AddRange(
                zonaRapi,
                zonaLogistica);

            db.CuadrosTarifarios.AddRange(
                cuadroRapi,
                cuadroLogistica);

            db.VersionesTarifarias.AddRange(
                VersionTarifaria.Crear(
                    RapiEnviosId,
                    cuadroRapi.Id,
                    zonaRapi.Id,
                    precioBase: 150m,
                    precioPorKg: 20m,
                    precioPorMetroCubico: 100m,
                    recargoUrgentePorcentaje: 25m,
                    bonificacionPorcentaje: 0m,
                    vigenciaDesde: DateTimeOffset.Parse("2026-01-01T00:00:00Z")),

                VersionTarifaria.Crear(
                    LogisticaSurId,
                    cuadroLogistica.Id,
                    zonaLogistica.Id,
                    precioBase: 180m,
                    precioPorKg: 25m,
                    precioPorMetroCubico: 120m,
                    recargoUrgentePorcentaje: 30m,
                    bonificacionPorcentaje: 0m,
                    vigenciaDesde: DateTimeOffset.Parse("2026-01-01T00:00:00Z")));

            await db.SaveChangesAsync(ct);
        }

        if (!await db.VersionesReglasOperativas
                .AnyAsync(v => v.OperadorId == RapiEnviosId, ct))
        {
            db.VersionesReglasOperativas.Add(
                VersionReglasOperativas.Crear(
                    operadorId: RapiEnviosId,
                    maximoIntentosEntrega: 3,
                    horasMinimasEntreIntentos: 24,
                    requierePruebaEntrega: true,
                    diasMaximosParaDevolucion: 7,
                    vigenciaDesde: DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        }

        if (!await db.VersionesReglasOperativas
                .AnyAsync(v => v.OperadorId == LogisticaSurId, ct))
        {
            db.VersionesReglasOperativas.Add(
                VersionReglasOperativas.Crear(
                    operadorId: LogisticaSurId,
                    maximoIntentosEntrega: 2,
                    horasMinimasEntreIntentos: 12,
                    requierePruebaEntrega: true,
                    diasMaximosParaDevolucion: 5,
                    vigenciaDesde: DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        }

        await db.SaveChangesAsync(ct);

        // Catálogo de motivos de no entrega de cada operador (RF 4 y 21). La app los muestra
        // y el servidor los busca por descripción al sincronizar un intento fallido.
        foreach (var version in await db.VersionesReglasOperativas.ToListAsync(ct))
        {
            var tieneMotivos = await db.Set<MotivoNoEntrega>()
                .AnyAsync(m => m.VersionReglasOperativasId == version.Id, ct);

            if (tieneMotivos)
                continue;

            db.Set<MotivoNoEntrega>().AddRange(
                MotivoNoEntrega.Crear(version.OperadorId, version.Id, "AUSENTE", "Destinatario ausente", requiereDevolucion: false),
                MotivoNoEntrega.Crear(version.OperadorId, version.Id, "DIRECCION", "Dirección incorrecta o inexistente", requiereDevolucion: false),
                MotivoNoEntrega.Crear(version.OperadorId, version.Id, "RECHAZADO", "Rechazado por el destinatario", requiereDevolucion: true),
                MotivoNoEntrega.Crear(version.OperadorId, version.Id, "INACCESIBLE", "Zona inaccesible", requiereDevolucion: false));
        }

        await db.SaveChangesAsync(ct);

        // Datos iniciales para probar la planificación de rutas.
        var operadores = await db.Operadores
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var operador in operadores)
        {
            var tieneRepartidor = await db.Repartidores
                .AnyAsync(r => r.OperadorId == operador.Id, ct);

            if (!tieneRepartidor)
            {
                db.Repartidores.Add(Repartidor.Crear(
                    operador.Id,
                    "Repartidor de prueba",
                    "099123456"));
            }

            var tieneVehiculo = await db.Vehiculos
                .AnyAsync(v => v.OperadorId == operador.Id, ct);

            if (!tieneVehiculo)
            {
                db.Vehiculos.Add(Vehiculo.Crear(
                    operador.Id,
                    "TEST1234",
                    500m,
                    3m));
            }
        }

        await db.SaveChangesAsync(ct);
    }
    
}
