using System;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.IntegrationTests;

/// <summary>
/// Aplica las migraciones sobre ultimamilla_integracion antes de la primera prueba, para que el
/// pipeline funcione contra un PostgreSQL vacío. Sin la variable de entorno no hace nada.
/// </summary>
internal static class InicializacionBase
{
    [ModuleInitializer]
    internal static void Migrar()
    {
        var conexion = Environment.GetEnvironmentVariable("ULTIMAMILLA_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(conexion)) return;

        // Misma protección que las pruebas: nunca migrar otra base.
        if (new NpgsqlConnectionStringBuilder(conexion).Database != "ultimamilla_integracion") return;

        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conexion).Options;
        using var db = new AppDbContext(opciones);
        db.Database.Migrate();
    }
}
