using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

/// <summary>Historial por bulto (RF 31). Se agrega desde Envio y no se modifica.</summary>
public sealed class EventoBultoConfiguration : IEntityTypeConfiguration<EventoBulto>
{
    public void Configure(EntityTypeBuilder<EventoBulto> builder)
    {
        builder.ToTable("eventos_bulto");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Identificacion).HasMaxLength(80).IsRequired();
        builder.Property(e => e.EstadoAnterior).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.EstadoNuevo).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Resultado).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Motivo).HasMaxLength(200);

        builder.HasIndex(e => new { e.OperadorId, e.EnvioId, e.OcurridoEn });
    }
}
