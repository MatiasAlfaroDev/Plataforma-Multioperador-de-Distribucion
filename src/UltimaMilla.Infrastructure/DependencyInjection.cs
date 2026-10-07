using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Interceptors;
using UltimaMilla.Infrastructure.Persistence.Seed;
using UltimaMilla.Infrastructure.Repositories;

namespace UltimaMilla.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra EF Core con PostgreSQL y las implementaciones de los contratos de Application.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IEnvioRepository, EnvioRepository>();
        services.AddScoped<ICuentaComercialRepository, CuentaComercialRepository>();
        services.AddScoped<IVersionTarifariaRepository, VersionTarifariaRepository>();
        services.AddScoped<IOperadorRepository, OperadorRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IZonaCoberturaRepository, ZonaCoberturaRepository>();
        return services;
    }

    /// <summary>
    /// Crea la base y carga los datos iniciales al arrancar, reintentando mientras PostgreSQL
    /// termina de levantar en Docker.
    /// PROVISORIO para el 8/10: antes del 15/10 se reemplaza EnsureCreated por migraciones
    /// con dotnet-ef (informe 6.6, RNF 6.4). Ver README, "Pendientes".
    /// </summary>
    public static async Task InicializarBaseDeDatosAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var intento = 1; ; intento++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                break;
            }
            catch (Exception) when (intento < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        await DbSeeder.SembrarAsync(db, ct);
    }
}
