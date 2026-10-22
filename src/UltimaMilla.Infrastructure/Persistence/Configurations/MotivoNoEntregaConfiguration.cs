using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class MotivoNoEntregaConfiguration
    : IEntityTypeConfiguration<MotivoNoEntrega>
{
    public void Configure(EntityTypeBuilder<MotivoNoEntrega> builder)
    {
        builder.ToTable("motivos_no_entrega");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Codigo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Descripcion)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(m => m.RequiereDevolucion)
            .IsRequired();

        builder.HasIndex(m => new
        {
            m.OperadorId,
            m.VersionReglasOperativasId,
            m.Codigo
        }).IsUnique();

        builder.HasOne<VersionReglasOperativas>()
            .WithMany()
            .HasForeignKey(m => m.VersionReglasOperativasId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}