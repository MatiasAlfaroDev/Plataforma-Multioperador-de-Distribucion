using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class VersionReglasOperativasConfiguration
    : IEntityTypeConfiguration<VersionReglasOperativas>
{
    public void Configure(EntityTypeBuilder<VersionReglasOperativas> builder)
    {
        builder.ToTable("versiones_reglas_operativas");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.MaximoIntentosEntrega)
            .IsRequired();

        builder.Property(v => v.HorasMinimasEntreIntentos)
            .IsRequired();

        builder.Property(v => v.RequierePruebaEntrega)
            .IsRequired();

        builder.Property(v => v.VigenciaDesde)
            .IsRequired();

        builder.HasOne<UltimaMilla.Domain.Organizacion.Operador>()
            .WithMany()
            .HasForeignKey(v => v.OperadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.OperadorId, v.VigenciaDesde });
    }
}