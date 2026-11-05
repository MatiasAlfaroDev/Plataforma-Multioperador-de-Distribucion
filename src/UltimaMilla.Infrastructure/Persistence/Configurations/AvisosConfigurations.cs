using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Avisos;
using UltimaMilla.Domain.Organizacion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

public sealed class SuscripcionAvisoConfiguration : IEntityTypeConfiguration<SuscripcionAviso>
{
    public void Configure(EntityTypeBuilder<SuscripcionAviso> builder)
    {
        builder.ToTable("suscripciones_aviso");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Url)
            .HasMaxLength(SuscripcionAviso.LargoMaximoUrl)
            .IsRequired();

        builder.Property(s => s.Secreto)
            .HasMaxLength(128)
            .IsRequired();

        // TiposEvento (List<string>) Npgsql lo guarda como text[] sin conversión.

        builder.HasOne<CuentaComercial>()
            .WithMany()
            .HasForeignKey(s => s.CuentaComercialId)
            .OnDelete(DeleteBehavior.Restrict);

        // ADR-002: el índice empieza por OperadorId.
        builder.HasIndex(s => new { s.OperadorId, s.CuentaComercialId });
    }
}

public sealed class EntregaAvisoConfiguration : IEntityTypeConfiguration<EntregaAviso>
{
    public void Configure(EntityTypeBuilder<EntregaAviso> builder)
    {
        builder.ToTable("entregas_aviso");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TipoEvento)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ClaveEvento)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Cuerpo).IsRequired(); // text

        builder.Property(e => e.Estado)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.UltimoError)
            .HasMaxLength(EntregaAviso.LargoMaximoError);

        builder.HasOne<SuscripcionAviso>()
            .WithMany()
            .HasForeignKey(e => e.SuscripcionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotencia (RNF 6.8): el mismo evento no genera dos avisos para la misma suscripción.
        builder.HasIndex(e => new { e.OperadorId, e.SuscripcionId, e.ClaveEvento }).IsUnique();

        // Historial del comercio y fallidos del operador.
        builder.HasIndex(e => new { e.OperadorId, e.CuentaComercialId, e.Estado });
    }
}
