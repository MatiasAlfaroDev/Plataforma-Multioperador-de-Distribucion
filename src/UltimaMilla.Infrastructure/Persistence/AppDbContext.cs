using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Configuracion;
using MassTransit;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Operador> Operadores => Set<Operador>();
    public DbSet<Comercio> Comercios => Set<Comercio>();
    public DbSet<CuentaComercial> CuentasComerciales => Set<CuentaComercial>();
    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<ZonaCobertura> ZonasCobertura => Set<ZonaCobertura>();
    public DbSet<CuadroTarifario> CuadrosTarifarios => Set<CuadroTarifario>();
    public DbSet<VersionTarifaria> VersionesTarifarias => Set<VersionTarifaria>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Toma todas las clases IEntityTypeConfiguration de la carpeta Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // tablas del outbox de MassTransit (RNF 6.8): el mensaje se guarda en la misma transacción que el cambio
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
