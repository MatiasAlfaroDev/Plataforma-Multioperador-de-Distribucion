using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class IntentoEntregaConfiguration : IEntityTypeConfiguration<IntentoEntrega>
{
    public void Configure(EntityTypeBuilder<IntentoEntrega> builder)
    {
        builder.ToTable("intentos_entrega");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ReferenciaEvidenciaFallida)
            .HasMaxLength(1000);

        builder.HasIndex(i => new { i.EnvioId, i.Numero })
            .IsUnique();

        builder.HasIndex(i => new { i.OperadorId, i.OcurridoEn });

        builder.HasOne(i => i.PruebaEntrega)
            .WithOne()
            .HasForeignKey<PruebaEntrega>("IntentoEntregaId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
