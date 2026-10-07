using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class ZonaCoberturaConfiguration
    : IEntityTypeConfiguration<ZonaCobertura>
{
    public void Configure(EntityTypeBuilder<ZonaCobertura> builder)
    {
        builder.ToTable("zonas_cobertura");
        builder.HasKey(z => z.Id);

        builder.Property(z => z.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(z => z.Departamento)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne<Operador>()
            .WithMany()
            .HasForeignKey(z => z.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(z => new { z.OperadorId, z.Nombre })
            .IsUnique();
    }
}

public sealed class CuadroTarifarioConfiguration
    : IEntityTypeConfiguration<CuadroTarifario>
{
    public void Configure(EntityTypeBuilder<CuadroTarifario> builder)
    {
        builder.ToTable("cuadros_tarifarios");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasOne<Operador>()
            .WithMany()
            .HasForeignKey(c => c.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.OperadorId, c.Nombre })
            .IsUnique();
    }
}

public sealed class VersionTarifariaConfiguration
    : IEntityTypeConfiguration<VersionTarifaria>
{
    public void Configure(EntityTypeBuilder<VersionTarifaria> builder)
    {
        builder.ToTable("versiones_tarifarias");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.PrecioBase)
            .HasPrecision(12, 2);

        builder.Property(v => v.PrecioPorKg)
            .HasPrecision(12, 2);

        builder.Property(v => v.PrecioPorMetroCubico)
            .HasPrecision(12, 2);

        builder.Property(v => v.RecargoUrgentePorcentaje)
            .HasPrecision(5, 2);

        builder.Property(v => v.BonificacionPorcentaje)
            .HasPrecision(5, 2);

        builder.HasOne<Operador>()
            .WithMany()
            .HasForeignKey(v => v.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CuadroTarifario>()
            .WithMany()
            .HasForeignKey(v => v.CuadroTarifarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ZonaCobertura>()
            .WithMany()
            .HasForeignKey(v => v.ZonaCoberturaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new
        {
            v.OperadorId,
            v.CuadroTarifarioId,
            v.ZonaCoberturaId,
            v.VigenciaDesde
        });
    }
}