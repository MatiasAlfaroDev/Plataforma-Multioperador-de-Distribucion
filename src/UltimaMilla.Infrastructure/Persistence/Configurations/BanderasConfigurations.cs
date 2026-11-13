using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Organizacion;
using UltimaMilla.Domain.Plataforma;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class BanderaFuncionalidadConfiguration : IEntityTypeConfiguration<BanderaFuncionalidad>
{
    public void Configure(EntityTypeBuilder<BanderaFuncionalidad> builder)
    {
        builder.ToTable("banderas_funcionalidad");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Nombre).HasMaxLength(64).IsRequired();
        builder.Property(b => b.ModificadoPor).HasMaxLength(256);
        builder.HasOne<Operador>().WithMany().HasForeignKey(b => b.OperadorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => new { b.OperadorId, b.Nombre }).IsUnique();
    }
}

public sealed class CambioBanderaConfiguration : IEntityTypeConfiguration<CambioBandera>
{
    public void Configure(EntityTypeBuilder<CambioBandera> builder)
    {
        builder.ToTable("cambios_bandera");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nombre).HasMaxLength(64).IsRequired();
        builder.Property(c => c.ModificadoPor).HasMaxLength(256).IsRequired();
        builder.HasIndex(c => c.ModificadoEn);
    }
}
