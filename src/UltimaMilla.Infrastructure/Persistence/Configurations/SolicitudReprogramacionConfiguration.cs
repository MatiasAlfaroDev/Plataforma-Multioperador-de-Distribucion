using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class SolicitudReprogramacionConfiguration
    : IEntityTypeConfiguration<SolicitudReprogramacion>
{
    public void Configure(EntityTypeBuilder<SolicitudReprogramacion> builder)
    {
        builder.ToTable("solicitudes_reprogramacion");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.OperadorId)
            .IsRequired();

        builder.Property(s => s.EnvioId)
            .IsRequired();

        builder.Property(s => s.SolicitadaEn)
            .IsRequired();

        builder.Property(s => s.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.AtendidaEn);

        builder.HasOne<Envio>()
            .WithMany()
            .HasForeignKey(s => s.EnvioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.OperadorId, s.Estado });

        builder.HasIndex(s => s.EnvioId)
            .IsUnique()
            .HasFilter("\"Estado\" = 'Pendiente'");
    }
}