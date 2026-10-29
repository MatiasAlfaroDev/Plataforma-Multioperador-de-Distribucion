using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MassTransit;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Interceptors;
using UltimaMilla.Infrastructure.Persistence.Seed;
using UltimaMilla.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using UltimaMilla.Infrastructure.Identidad;
using Microsoft.Extensions.Configuration;

namespace UltimaMilla.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra EF Core con PostgreSQL, MassTransit con RabbitMQ y las implementaciones de los contratos de Application.
    /// rabbitMq: cadena amqp://usuario:clave@host:5672
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string rabbitMq)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddSingleton<TenantSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>()));

        services.AddIdentityCore<Usuario>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();
        services.AddScoped<IEnvioRepository, EnvioRepository>();
        services.AddScoped<IEventoMovilRepository, EventoMovilRepository>();
        services.AddScoped<ICuentaComercialRepository, CuentaComercialRepository>();
        services.AddScoped<IVersionTarifariaRepository, VersionTarifariaRepository>();
        services.AddScoped<IVersionReglasOperativasRepository, VersionReglasOperativasRepository>();
        services.AddScoped<IMotivoNoEntregaRepository, MotivoNoEntregaRepository>();
        services.AddScoped<ISolicitudReprogramacionRepository, SolicitudReprogramacionRepository>();
        services.AddScoped<IOperadorRepository, OperadorRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IZonaCoberturaRepository, ZonaCoberturaRepository>();
        services.AddScoped<IRepartidorRepository, RepartidorRepository>();
        services.AddScoped<IVehiculoRepository, VehiculoRepository>();
        services.AddScoped<IHojaDeRutaRepository, HojaDeRutaRepository>();
        services.AddScoped<IPublicadorDeEventos, PublicadorDeEventos>();

        // El outbox (RNF 6.8) guarda el mensaje en la misma transacción y un servicio en segundo plano
        // de cada réplica lo entrega a RabbitMQ; UsePostgres hace que las dos réplicas no se pisen (bloqueo por fila).
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<AppDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });
            
            // Sin RabbitMQ la API sigue sirviendo y acumula en el outbox, así que el bus no entra en /ready (Traefik la sacaría del pool); queda en /health.
            x.ConfigureHealthCheckOptions(o => o.Tags.Remove("ready"));
            
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(new Uri(rabbitMq));
                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }

    /// <summary>
    /// Aplica las migraciones pendientes y carga los datos iniciales.
    /// Se ejecuta únicamente desde el servicio inicializador de Docker,
    /// evitando que las réplicas de la API modifiquen el esquema en paralelo.
    /// </summary>
    public static async Task InicializarBaseDeDatosAsync(
    this IServiceProvider services,
    CancellationToken ct = default)
{
    using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.HabilitarAccesoInicializacion();

        await db.Database.MigrateAsync(ct);

    // Datos iniciales del negocio.
    await DbSeeder.SembrarAsync(db, ct);

    // Usuarios, roles y membresías.
    await IdentitySeeder.SembrarAsync(
        scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
        db,
        scope.ServiceProvider.GetRequiredService<IConfiguration>(),
        ct);
}
}
