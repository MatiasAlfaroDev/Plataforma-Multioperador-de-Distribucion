using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class ResultadoBultoIntentoConfiguration
    : IEntityTypeConfiguration<ResultadoBultoIntento>
{
    public void Configure(EntityTypeBuilder<ResultadoBultoIntento> builder)
    {
        builder.ToTable("resultados_bultos_intento");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Entregado)
            .IsRequired();

        builder.Property(r => r.ReferenciaEvidenciaFallo)
            .HasMaxLength(1000);

        builder.HasIndex(r => new
        {
            r.IntentoEntregaId,
            r.BultoId
        }).IsUnique();

        builder.HasOne<Bulto>()
            .WithMany()
            .HasForeignKey(r => r.BultoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MotivoNoEntrega>()
            .WithMany()
            .HasForeignKey(r => r.MotivoNoEntregaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}