using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class EventoEnvioConfiguration : IEntityTypeConfiguration<EventoEnvio>
{
    public void Configure(EntityTypeBuilder<EventoEnvio> builder)
    {
        builder.ToTable("eventos_envio");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id)
                .ValueGeneratedNever();

        builder.Property(e => e.EstadoAnterior)
            .HasConversion<string>().HasMaxLength(32);

        builder.Property(e => e.EstadoNuevo)
            .HasConversion<string>().HasMaxLength(32);

        builder.Property(e => e.Disparador)
            .HasConversion<string>().HasMaxLength(40);

        builder.Property(e => e.Origen)
            .HasMaxLength(100).IsRequired();

        builder.Property(e => e.Responsable)
            .HasMaxLength(200).IsRequired();

        builder.Property(e => e.Justificacion)
            .HasMaxLength(1000);

        builder.Property(e => e.Latitud).HasPrecision(9, 6);
        builder.Property(e => e.Longitud).HasPrecision(9, 6);

        builder.HasIndex(e => new { e.OperadorId, e.EnvioId, e.OcurridoEn });
    }
}
