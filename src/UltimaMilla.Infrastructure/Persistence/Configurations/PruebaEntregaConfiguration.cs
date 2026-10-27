using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class PruebaEntregaConfiguration : IEntityTypeConfiguration<PruebaEntrega>
{
    public void Configure(EntityTypeBuilder<PruebaEntrega> builder)
    {
        builder.ToTable("pruebas_entrega");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .ValueGeneratedNever();
        builder.Property(p => p.NombreReceptor)
            .HasMaxLength(200).IsRequired();

        builder.Property(p => p.DocumentoReceptor)
            .HasMaxLength(50);

        builder.Property(p => p.ReferenciaFirma)
            .HasMaxLength(1000);

        builder.Property(p => p.ReferenciaFotografia)
            .HasMaxLength(1000);

        builder.Property(p => p.Latitud).HasPrecision(9, 6);
        builder.Property(p => p.Longitud).HasPrecision(9, 6);
    }
}
