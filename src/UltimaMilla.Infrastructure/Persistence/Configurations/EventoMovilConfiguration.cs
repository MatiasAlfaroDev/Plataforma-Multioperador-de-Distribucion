using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

/// <summary>Eventos sincronizados desde la app del repartidor.</summary>
public sealed class EventoMovilConfiguration : IEntityTypeConfiguration<EventoMovil>
{
    public void Configure(EntityTypeBuilder<EventoMovil> builder)
    {
        builder.ToTable("eventos_movil");

        // La clave es el Id que generó el teléfono: la base rechaza un segundo INSERT con el mismo Id,
        // así un reintento nunca registra dos veces la misma entrega (idempotencia).
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Motivo).HasMaxLength(200);
        builder.Property(e => e.Receptor).HasMaxLength(200);

        builder.HasOne<Envio>().WithMany().HasForeignKey(e => e.EnvioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EnvioId);
        // Las consultas por inquilino empiezan por OperadorId (ADR-002).
        builder.HasIndex(e => new { e.OperadorId, e.OcurridoEn });
    }
}
