using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class OperadorConfiguration : IEntityTypeConfiguration<Operador>
{
    public void Configure(EntityTypeBuilder<Operador> builder)
    {
        builder.ToTable("operadores");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(o => o.ColorPrimario).HasMaxLength(9);
        builder.Property(o => o.EmailContacto).HasMaxLength(200);
    }
}

public sealed class ComercioConfiguration : IEntityTypeConfiguration<Comercio>
{
    public void Configure(EntityTypeBuilder<Comercio> builder)
    {
        builder.ToTable("comercios");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Rut).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Rut).IsUnique();
    }
}

public sealed class CuentaComercialConfiguration : IEntityTypeConfiguration<CuentaComercial>
{
    public void Configure(EntityTypeBuilder<CuentaComercial> builder)
    {
        builder.ToTable("cuentas_comerciales");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Estado).HasConversion<string>().HasMaxLength(30).IsRequired();        
        builder.HasOne<Operador>().WithMany().HasForeignKey(c => c.OperadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Comercio>().WithMany().HasForeignKey(c => c.ComercioId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(c => c.DireccionRecepcion, d =>
        {
            d.Property(x => x.Calle)
                .HasColumnName("recepcion_calle")
                .HasMaxLength(200)
                .IsRequired();

            d.Property(x => x.Numero)
                .HasColumnName("recepcion_numero")
                .HasMaxLength(20);

            d.Property(x => x.Localidad)
                .HasColumnName("recepcion_localidad")
                .HasMaxLength(100)
                .IsRequired();

            d.Property(x => x.Departamento)
                .HasColumnName("recepcion_departamento")
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.Navigation(c => c.DireccionRecepcion)
            .IsRequired(false);

        // Un comercio tiene una sola cuenta con cada operador.
        builder.HasIndex(c => new { c.OperadorId, c.ComercioId }).IsUnique();
    }
}
