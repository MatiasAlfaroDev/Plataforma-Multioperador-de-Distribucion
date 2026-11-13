using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

/// <summary>Discrepancias entre lo declarado y lo escaneado al recibir en depósito (RF 10).</summary>
public sealed class DiscrepanciaRecepcionConfiguration : IEntityTypeConfiguration<DiscrepanciaRecepcion>
{
    public void Configure(EntityTypeBuilder<DiscrepanciaRecepcion> builder)
    {
        builder.ToTable("discrepancias_recepcion");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Identificacion).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Responsable).HasMaxLength(200).IsRequired();

        builder.HasOne<Envio>().WithMany().HasForeignKey(d => d.EnvioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.OperadorId, d.EnvioId, d.RegistradaEn });
    }
}
