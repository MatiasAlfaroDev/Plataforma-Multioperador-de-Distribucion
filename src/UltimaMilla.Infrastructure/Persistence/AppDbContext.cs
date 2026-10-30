using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Rutas;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using UltimaMilla.Infrastructure.Identidad;
using UltimaMilla.Application.Seguridad;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext? tenantContext = null)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options)
{
    private readonly ITenantContext? _tenantContext = tenantContext;

    public Guid? OperadorActualId => _tenantContext?.OperadorId;

    public bool AccesoInicializacion { get; private set; }

    public void HabilitarAccesoInicializacion()
    {
        AccesoInicializacion = true;
    }

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
    public DbSet<SolicitudReprogramacion> SolicitudesReprogramacion => Set<SolicitudReprogramacion>();
    public DbSet<EventoMovil> EventosMovil => Set<EventoMovil>();
    public DbSet<ZonaCobertura> ZonasCobertura => Set<ZonaCobertura>();
    public DbSet<CuadroTarifario> CuadrosTarifarios => Set<CuadroTarifario>();
    public DbSet<VersionTarifaria> VersionesTarifarias => Set<VersionTarifaria>();
    public DbSet<VersionReglasOperativas> VersionesReglasOperativas => Set<VersionReglasOperativas>();
    public DbSet<MembresiaUsuario> MembresiasUsuarios=> Set<MembresiaUsuario>();
    public DbSet<DispositivoMovil> DispositivosMoviles => Set<DispositivoMovil>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración de las tablas de ASP.NET Core Identity.
        base.OnModelCreating(modelBuilder);

        // Configuraciones existentes de las entidades de negocio.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        modelBuilder.Entity<CuentaComercial>()
            .HasQueryFilter(c =>
                AccesoInicializacion ||
                c.OperadorId == OperadorActualId);

        modelBuilder.Entity<Envio>()
            .HasQueryFilter(e =>
                AccesoInicializacion ||
                e.OperadorId == OperadorActualId);

        modelBuilder.Entity<CuadroTarifario>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<Repartidor>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<Vehiculo>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        // Teléfonos para push: cada operador ve solo los suyos.
        modelBuilder.Entity<DispositivoMovil>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<VersionReglasOperativas>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<VersionTarifaria>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<ZonaCobertura>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
               x.OperadorId == OperadorActualId);

        modelBuilder.Entity<EventoMovil>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<HojaDeRuta>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<MotivoNoEntrega>()
            .HasQueryFilter(x =>
                AccesoInicializacion ||
                x.OperadorId == OperadorActualId);

        modelBuilder.Entity<SolicitudReprogramacion>()
            .HasQueryFilter(s =>
                AccesoInicializacion ||
                s.OperadorId == OperadorActualId);
        // Tablas del outbox de MassTransit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
