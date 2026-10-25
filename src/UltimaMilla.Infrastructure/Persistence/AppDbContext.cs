using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Rutas;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Identidad;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Operador> Operadores => Set<Operador>();
    public DbSet<Comercio> Comercios => Set<Comercio>();
    public DbSet<CuentaComercial> CuentasComerciales => Set<CuentaComercial>();
    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<EventoEnvio> EventosEnvio => Set<EventoEnvio>();
    public DbSet<Repartidor> Repartidores => Set<Repartidor>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<HojaDeRuta> HojasDeRuta => Set<HojaDeRuta>();
    public DbSet<ParadaRuta> ParadasRuta => Set<ParadaRuta>();
    public DbSet<IntentoEntrega> IntentosEntrega => Set<IntentoEntrega>();
    public DbSet<PruebaEntrega> PruebasEntrega => Set<PruebaEntrega>();
    public DbSet<EventoMovil> EventosMovil => Set<EventoMovil>();
    public DbSet<ZonaCobertura> ZonasCobertura => Set<ZonaCobertura>();
    public DbSet<CuadroTarifario> CuadrosTarifarios => Set<CuadroTarifario>();
    public DbSet<VersionTarifaria> VersionesTarifarias => Set<VersionTarifaria>();
    public DbSet<VersionReglasOperativas> VersionesReglasOperativas => Set<VersionReglasOperativas>();
    public DbSet<MembresiaUsuario> MembresiasUsuarios=> Set<MembresiaUsuario>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración de las tablas de ASP.NET Core Identity.
        base.OnModelCreating(modelBuilder);

        // Configuraciones existentes de las entidades de negocio.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        // Tablas del outbox de MassTransit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
