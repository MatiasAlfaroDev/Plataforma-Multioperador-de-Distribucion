using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Identity;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Interceptors;
using UltimaMilla.Infrastructure.Persistence.Seed;
using UltimaMilla.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;

namespace UltimaMilla.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra EF Core con PostgreSQL y las implementaciones
    /// de los contratos de Application.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Permite acceder al HttpContext de la petición actual.
        services.AddHttpContextAccessor();

        // ---------------------------------------------------------
        // MULTITENANCY
        // ---------------------------------------------------------

        // Una única instancia de TenantContext por scope/petición.
        services.AddScoped<TenantContext>();

        // ITenantContext permite CONSULTAR cuál es el tenant actual.
        services.AddScoped<ITenantContext>(sp =>
            sp.GetRequiredService<TenantContext>());

        // ITenantScope permite ESTABLECER explícitamente un tenant
        // en procesos internos, por ejemplo Seeder o Worker.
        services.AddScoped<ITenantScope>(sp =>
            sp.GetRequiredService<TenantContext>());

        // ---------------------------------------------------------
        // INTERCEPTORES DE EF CORE
        // ---------------------------------------------------------

        // Completa CreadoEn y ActualizadoEn.
        services.AddSingleton<AuditableEntityInterceptor>();

        // Valida que las entidades tenant que se guardan
        // pertenezcan al operador actual.
        services.AddScoped<TenantEntityInterceptor>();

        // ---------------------------------------------------------
        // ENTITY FRAMEWORK + POSTGRESQL
        // ---------------------------------------------------------

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<TenantEntityInterceptor>()));


        // ---------------------------------------------------------
        // ASP.NET CORE IDENTITY
        // ---------------------------------------------------------

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Contraseñas.
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;

            // Usuarios.
            options.User.RequireUniqueEmail = true;

            // Bloqueo ante intentos fallidos.
            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();    

        // ---------------------------------------------------------
        // REPOSITORIOS
        // ---------------------------------------------------------

        services.AddScoped<IEnvioRepository, EnvioRepository>();
        services.AddScoped<ICuentaComercialRepository, CuentaComercialRepository>();
        services.AddScoped<IOperadorRepository, OperadorRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Crea la base y carga los datos iniciales al arrancar,
    /// reintentando mientras PostgreSQL termina de levantar en Docker.
    ///
    /// PROVISORIO para el 8/10: antes del 15/10 se reemplaza
    /// EnsureCreated por migraciones con dotnet-ef
    /// (informe 6.6, RNF 6.4).
    /// Ver README, "Pendientes".
    /// </summary>
    public static async Task InicializarBaseDeDatosAsync(
        this IServiceProvider services,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();

        var db =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantScope =
            scope.ServiceProvider.GetRequiredService<ITenantScope>();

        for (var intento = 1; ; intento++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                break;
            }
            catch (Exception) when (intento < 10)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(3),
                    ct);
            }
        }

        await DbSeeder.SembrarAsync(
            db,
            tenantScope,
            ct);var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

        await IdentitySeeder.SembrarAsync(
            userManager,
            db);

    
    }
}