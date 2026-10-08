using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Infrastructure.Identity;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AppDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Entidades del dominio.
    public DbSet<Operador> Operadores => Set<Operador>();
    public DbSet<Comercio> Comercios => Set<Comercio>();
    public DbSet<CuentaComercial> CuentasComerciales => Set<CuentaComercial>();
    public DbSet<Envio> Envios => Set<Envio>();

    // Relaciones de usuarios.
    public DbSet<UsuarioOperador> UsuariosOperadores =>
        Set<UsuarioOperador>();

    public DbSet<UsuarioComercio> UsuariosComercios =>
        Set<UsuarioComercio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tablas de ASP.NET Core Identity.
        base.OnModelCreating(modelBuilder);

        // Configuraciones existentes del dominio.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        // --------------------------------------------------
        // USUARIO - OPERADOR
        // --------------------------------------------------

        modelBuilder.Entity<UsuarioOperador>(entity =>
        {
            entity.HasKey(x => new
            {
                x.UsuarioId,
                x.OperadorId
            });

            entity.HasOne(x => x.Usuario)
                .WithMany(x => x.Operadores)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Operador>()
                .WithMany()
                .HasForeignKey(x => x.OperadorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.Rol)
                .HasMaxLength(50)
                .IsRequired();
        });

        // --------------------------------------------------
        // USUARIO - COMERCIO
        // --------------------------------------------------

        modelBuilder.Entity<UsuarioComercio>(entity =>
        {
            entity.HasKey(x => new
            {
                x.UsuarioId,
                x.ComercioId
            });

            entity.HasOne(x => x.Usuario)
                .WithMany(x => x.Comercios)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Comercio>()
                .WithMany()
                .HasForeignKey(x => x.ComercioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.Rol)
                .HasMaxLength(50)
                .IsRequired();
        });

        // --------------------------------------------------
        // MULTITENANCY EXISTENTE
        // --------------------------------------------------

        modelBuilder.Entity<CuentaComercial>()
            .HasQueryFilter(cuenta =>
                _tenantContext.OperadorId.HasValue &&
                cuenta.OperadorId == _tenantContext.OperadorId.Value);

        modelBuilder.Entity<Envio>()
            .HasQueryFilter(envio =>
                _tenantContext.OperadorId.HasValue &&
                envio.OperadorId == _tenantContext.OperadorId.Value);
    }
}