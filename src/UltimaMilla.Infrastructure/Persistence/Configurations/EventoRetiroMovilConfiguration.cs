using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Retiros;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class EventoRetiroMovilConfiguration
    : IEntityTypeConfiguration<EventoRetiroMovil>
{
    public void Configure(
        EntityTypeBuilder<EventoRetiroMovil> builder)
    {
        builder.ToTable("eventos_retiro_movil");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Tipo)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.MotivoFallo)
            .HasMaxLength(500);

        builder.Property(e => e.ReferenciaFotografia)
            .HasMaxLength(500);

        builder.Property(e => e.ReferenciaFirma)
            .HasMaxLength(500);

        builder.Property(e => e.IdentificacionesBultos)
            .HasColumnType("text[]");

        builder.HasOne<Retiro>()
            .WithMany()
            .HasForeignKey(e => e.RetiroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new
        {
            e.OperadorId,
            e.RetiroId,
            e.OcurridoEn
        });
    }
}