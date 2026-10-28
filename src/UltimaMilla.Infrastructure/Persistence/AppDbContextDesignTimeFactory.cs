using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UltimaMilla.Infrastructure.Persistence;

/// <summary>
/// Solo la usa `dotnet ef` (migraciones). Así la herramienta crea el AppDbContext directo,
/// sin arrancar la API entera (que necesita Redis, RabbitMQ y el caché para construirse).
/// No se usa en tiempo de ejecución: ahí el contexto lo arma AddInfrastructure.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Para generar una migración no hace falta conectarse: alcanza con saber que es PostgreSQL.
        var conexion = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=ultimamilla;Username=ultimamilla;Password=ultimamilla_dev";

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion)
            .Options;

        return new AppDbContext(opciones);
    }
}